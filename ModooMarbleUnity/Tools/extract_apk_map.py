"""Bounded, execution-free extraction of map0, guide text and Korean font.

Run: python Tools/extract_apk_map.py [APK path]
No APK code is run, URLs followed, or arbitrary ZIP paths extracted.
"""
from pathlib import Path
import csv
import hashlib
import json
import re
import struct
import sys
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
APK = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk")
OUT = ROOT / "Documentation" / "apk"
STREAM = ROOT / "Assets" / "StreamingAssets"
FONT = ROOT / "Assets" / "Marble" / "Resources" / "MarbleKorean.ttf"
STRIDE = 277


def save_json(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def keyed_strings(data):
    """Find bounded UTF-8 key/value pairs; keep duplicates with source offsets.

    ADD_ALL's full record schema is NOT claimed recovered. Accepted candidates
    have matching length-prefixed ASCII keys and valid bounded UTF-8 values.
    """
    found = {}
    for m in re.finditer(rb"[A-Za-z]", data):
        start = m.start()
        if start < 4:
            continue
        key_length = struct.unpack_from("<I", data, start - 4)[0]
        end = start + key_length
        if not 2 <= key_length <= 60 or end + 4 > len(data):
            continue
        key_bytes = data[start:end]
        if not re.fullmatch(rb"[A-Za-z][A-Za-z0-9_]+", key_bytes):
            continue
        length = struct.unpack_from("<I", data, end)[0]
        if not 0 < length <= 8192 or end + 4 + length > len(data):
            continue
        try:
            value = data[end + 4:end + 4 + length].decode("utf-8")
        except UnicodeDecodeError:
            continue
        if "\0" in value:
            continue
        key = key_bytes.decode("ascii")
        found.setdefault(key, []).append({"key": key, "keyOffset": start, "textOffset": end + 4, "textBytes": length, "text": value})
    return found


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    STREAM.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(APK.read_bytes()).hexdigest()
    sources = []
    with zipfile.ZipFile(APK) as z:
        def read(name):
            data = z.read(name)
            sources.append({"entry": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()})
            return data

        labels = keyed_strings(read("assets/Data/ADD_ALL"))
        data = read("assets/Data/RgnInfo0.dat")
        assert len(data) == STRIDE * 32
        tiles = []
        kinds = {0: "City", 1: "ResortType1", 2: "ResortType2", 3: "Chance", 4: "Start", 5: "Island", 6: "Travel", 7: "Bonus", 9: "Olympics"}
        for i in range(32):
            b = data[i * STRIDE:(i + 1) * STRIDE]
            index = struct.unpack_from("<i", b, 4)[0]
            assert index == i
            key = b[8:54].split(b"\0")[0].decode("ascii")
            assert key in labels
            # Map0 uses the original unprefixed label namespace. Other map
            # namespaces use prefixed keys. Keep every duplicate in evidence.
            label = labels[key][0]
            previous, following = struct.unpack_from("<ii", b, 74)
            assert previous == (i - 1) % 32 and following == (i + 1) % 32
            values = list(struct.unpack_from("<20q", b, 115))
            kind = struct.unpack_from("<i", b, 107)[0]
            tiles.append({
                "index": index, "nameKey": key, "name": label["text"],
                "nameSourceOffset": label["textOffset"], "recordOffset": i * STRIDE,
                "previousIndex": previous, "nextIndex": following,
                "colorRgba": b[54:70].split(b"\0")[0].decode("ascii"),
                "lineRaw": struct.unpack_from("<i", b, 90)[0],
                "groupRaw": b[106], "kindRaw": kind,
                "kindCandidate": kinds.get(kind, "Unknown"),
                "maskRaw": struct.unpack_from("<i", b, 111)[0],
                "rawFinancialValues": values,
                "candidateFinancialRows": [
                    {"propertyKindCandidate": name, "buyCandidate": values[j * 4], "sellCandidate": values[j * 4 + 1], "tollCandidate": values[j * 4 + 2], "takeoverCandidate": values[j * 4 + 3]}
                    for j, name in enumerate(["Land", "Villa", "Building", "Hotel", "Landmark"])
                ],
            })
        document = {"sourceApkSha256": digest, "sourceEntry": "assets/Data/RgnInfo0.dat", "recordStride": STRIDE, "status": "Static records and labels observed; kind/financial semantics are inferred", "tiles": tiles}
        save_json(STREAM / "original-map.json", document)
        save_json(OUT / "map0-records.json", document)
        selected = {key: rows for key, rows in labels.items() if re.fullmatch(r"s(?:329[0-9]|330[0-9]|3310|3311)(?:_[0-9]+)?", key)}
        save_json(OUT / "guide-strings.json", {"source": "assets/Data/ADD_ALL", "status": "Observed first-party packaged text, not runtime verification", "strings": selected})
        save_json(OUT / "map0-labels.json", {t["nameKey"]: labels[t["nameKey"]] for t in tiles})
        with (OUT / "map0-financial-candidates.csv").open("w", encoding="utf-8-sig", newline="") as f:
            w = csv.writer(f)
            w.writerow(["index", "name", "kindRaw", "groupRaw", "propertyKindCandidate", "buyCandidate", "sellCandidate", "tollCandidate", "takeoverCandidate"])
            for t in tiles:
                for row in t["candidateFinancialRows"]:
                    w.writerow([t["index"], t["name"], t["kindRaw"], t["groupRaw"], *row.values()])
        for entry in ["assets/Script/Tutorial.lua", "assets/Script/Tutorial_bankrupt.lua", "assets/Script/toLuaScript/luaCiceroneAi.lua"]:
            b = read(entry)
            if b[:4] == b"N2Z@":
                expected = struct.unpack_from("<I", b, 4)[0]
                decoded = zlib.decompress(b[8:])
                assert len(decoded) == expected
                encoding = "utf-8" if Path(entry).name == "Tutorial.lua" else "cp949"
            else:
                decoded = b
                encoding = "cp949"
            text = decoded.decode(encoding)
            (OUT / Path(entry).name).write_text(text, encoding="utf-8")
            sources[-1].update({"decodedBytes": len(decoded), "sourceTextEncoding": encoding, "outputEncoding": "utf-8", "decodedSha256": hashlib.sha256(decoded).hexdigest()})
        raw_options = {}
        for name, fmt in [("Option", "<12q"), ("TourPrice", "<3i"), ("BeachPrice", "<3i")]:
            b = read("assets/Data/" + name + ".dat")
            raw_options[name] = list(struct.unpack(fmt, b))
        save_json(OUT / "option-values-raw.json", raw_options)
        font = read("assets/font/GodoB.ttf")
        FONT.parent.mkdir(parents=True, exist_ok=True)
        FONT.write_bytes(font)
    save_json(OUT / "source-manifest.json", {"apkPath": str(APK), "apkSha256": digest, "method": "Python zipfile + struct + zlib; no target execution or network", "sources": sources})
    print(json.dumps({"tiles": len(tiles), "guideKeys": len(selected), "font": str(FONT), "output": str(OUT)}, ensure_ascii=True))


if __name__ == "__main__":
    main()
