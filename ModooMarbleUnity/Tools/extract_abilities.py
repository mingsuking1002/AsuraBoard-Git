"""Execution-free, full-coverage ADD_ALL text catalog and bounded ItemList links.

This parser reads the complete repeated record format, not a strings/regex dump.
It never runs APK code, follows target URLs, or treats source text as instructions.
Ability tags and numeric-normalized sentence families are analyst search aids,
not claims that strings implement distinct abilities or remain live on servers.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
import hashlib
import json
from pathlib import Path
import re
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_APK = Path(r"C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk")
EXPECTED_SHA = "2850a586f6b0f7109e87ed040ace7f1e1af7da9c9fa247f483708578f9b084bc"
OUT = ROOT / "Documentation/abilities/apk-catalog"

# Literal text classifiers. They deliberately overlap: one string can describe
# several effects. The complete unfiltered catalog is retained for missed terms.
TAG_RULES = [
    ("bubble", "비눗방울·물방울", r"비눗방울|물방울"),
    ("bind", "속박·이동 금지·붙잡기", r"속박|가두|갇히|갇힌|이동\s*(?:불가|금지)|붙잡|나무\s*덩[쿨굴]|얼음\s*감옥|감옥\s*구속"),
    ("pull-push", "끌어당김·밀어내기", r"끌어|밀어내|밀쳐|자석|당겨|날려\s*보"),
    ("black-hole", "블랙홀·화이트홀", r"블랙홀|화이트홀|블랙\s*홀|화이트\s*홀"),
    ("movement", "이동·순간이동·포탈", r"이동|워프|포탈|포털|텔레포트|비행"),
    ("travel", "세계여행·비행·여행", r"세계\s*여행|우주\s*여행|비행|여행권|여행\s*권"),
    ("island", "무인도·탈출", r"무인도|탈출|감옥"),
    ("takeover", "인수·땅 빼앗기·소유권", r"인수|소유권|빼앗|빼앗|뺏|강제\s*매각|강제\s*판매"),
    ("construction", "건설·랜드마크·건물", r"건설|랜드마크|건물|별장|빌딩|호텔|철거|파괴"),
    ("toll", "통행료·배수", r"통행료|통행\s*료|배수|추가\s*징수"),
    ("money-steal", "마블 강탈·금액 이동", r"강탈|보유\s*마블|보유\s*금액|마블\s*(?:획득|지급|소모|지불|뺏)|돈을|머니|월급|자산"),
    ("dice", "주사위·더블·주사위 컨트롤", r"주사위|더블|홀짝|주컨|주사\s*위"),
    ("extra-turn", "추가 턴·턴 강제 종료", r"추가\s*(?:턴|행동|주사위)|턴\s*(?:강제\s*)?종료|한\s*번\s*더|연속\s*행동|다시\s*굴"),
    ("trap-install", "설치물·함정·영역", r"설치|트랩|함정|마법진|장판|구름|영역|지뢰|덩[쿨굴]|맹독|독안개"),
    ("defense", "방어·보호막·면역", r"방어|보호막|보호\s*막|면역|쉴드|실드|방패|무적|방어\s*드론"),
    ("ignore-defense", "방어 무시·관통", r"방어\s*무시|무시.*방어|관통|방어불가|방어\s*불가"),
    ("reflect", "반사·반격", r"반사|반격|되돌려"),
    ("seal-disable", "봉인·무효·능력 차단", r"봉인|무효|무력화|발동\s*취소|스킬\s*취소|능력\s*(?:차단|제거|취소)|능력치\s*감소"),
    ("cleanse", "해제·회복·충전", r"해제|회복|충전|치유|정화|디버프\s*제거"),
    ("copy-transform", "복사·복제·변신", r"복사|복제|변신|카피|도용|빼앗은\s*능력"),
    ("angel", "천사 카드·방어 카드", r"천사\s*카드|천사카드|방어\s*카드|포츈|포춘|찬스\s*카드"),
    ("monopoly", "독점·승리 방해", r"독점|승리|패배|파산"),
    ("map-change", "맵·블록 변화", r"블록|얼음|빙판|수중|라인\s*(?:상승|하강)|들어\s*올|밀어\s*올|라인을\s*회전|맵\s*회전"),
    ("debuff", "디버프·상태 이상", r"디버프|해로운\s*효과|상태\s*이상|저주|불면증|감전|수면|냉동|빙결|중독|어둠|침묵|사슬|스턴"),
    ("stat-modifier", "능력치·발동률 변화", r"능력치|발동률|발동\s*확률|확률\s*(?:증가|감소)|스킬\s*확률"),
]
TAGS = [(key, label, re.compile(pattern)) for key, label, pattern in TAG_RULES]
STRONG_TAGS = {"bubble", "bind", "pull-push", "black-hole", "trap-install", "ignore-defense", "reflect", "seal-disable", "copy-transform", "debuff"}
TRIGGER = re.compile(r"(?:도착|건설|인수|이동|시작|종료|통과|지나|발동|굴|실패|성공|보유|획득|방어|공격|상대|내\s*땅|내\s*지역|내\s*턴|확률|##skill|%[0-9]*d|\d+\s*%)")
PROMO = re.compile(r"(?:이벤트\s*기간|합성|패키지|쿠폰|교환소|접속\s*시|출석|우편함|카드팩|카드\s*팩|선택\s*큐브|골드\s*구매|한정\s*판매|획득\s*가능|확정\s*획득|뽑기|[0-9]+종\s*(?:중|만|등장)|[0-9]+종.{0,20}(?:포함|획득|등장)|\[[0-9]+/[0-9]+)")
STATUS_KEY = re.compile(r"^(?:buff_(?:title|desc)|trapinfo_(?:title|desc))_\d+$")
PLACEHOLDER = re.compile(r"##[^#\r\n]+##|\{\d+(?::[^}]+)?\}|%[-+0-9.*]*[diufsg]|\[[A-Za-z_]+\]")
STYLE = re.compile(r"</?style(?:=[^>]*)?>|<br\s*/?>", re.I)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def save_json(path: Path, obj) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def save_jsonl(path: Path, rows) -> None:
    with path.open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")


def parse_add_all(data: bytes):
    """Parse every byte as records: key,text,font,size,h,v,color,shadow,outline.

    Strings have little-endian uint32 byte lengths and strict UTF-8 bytes.
    Every field is source-bound. Empty texts and duplicate keys are retained.
    Limits are defensive and not record-count truncation limits.
    """
    p = 0
    rows = []

    def text_field(max_bytes=1_000_000):
        nonlocal p
        if p + 4 > len(data):
            raise ValueError(f"Truncated length at {p}")
        length = struct.unpack_from("<I", data, p)[0]
        p += 4
        start = p
        if length > max_bytes or p + length > len(data):
            raise ValueError(f"Invalid field length {length} at {p - 4}")
        value = data[p:p + length].decode("utf-8", errors="strict")
        p += length
        return value, start, length

    while p < len(data):
        record_start = p
        key, key_offset, key_bytes = text_field(1024)
        value, text_offset, text_bytes = text_field()
        font, _, _ = text_field(1024)
        if p + 4 > len(data):
            raise ValueError(f"Truncated font size at {p}")
        font_size = struct.unpack_from("<i", data, p)[0]
        p += 4
        horizontal, vertical, color, shadow, outline = [text_field()[0] for _ in range(5)]
        if not key or "\0" in key:
            raise ValueError(f"Empty or NUL-containing key at {record_start}")
        row = {
            "recordId": f"ADD-{len(rows):06d}", "ordinal": len(rows), "key": key,
            "recordOffset": record_start, "recordEndOffset": p, "keyOffset": key_offset,
            "keyBytes": key_bytes, "textOffset": text_offset, "textBytes": text_bytes,
            "text": value, "font": font, "fontSize": font_size,
            "horizontalAlign": horizontal, "verticalAlign": vertical,
            "colorRaw": color, "shadowRaw": shadow, "outlineRaw": outline,
        }
        # The round trip validates the complete observed record shape.
        parts = []
        for string in [key, value, font]:
            encoded = string.encode("utf-8")
            parts.extend((struct.pack("<I", len(encoded)), encoded))
        parts.append(struct.pack("<i", font_size))
        for string in [horizontal, vertical, color, shadow, outline]:
            encoded = string.encode("utf-8")
            parts.extend((struct.pack("<I", len(encoded)), encoded))
        if b"".join(parts) != data[record_start:p]:
            raise ValueError(f"Record round-trip mismatch at {record_start}")
        rows.append(row)
    assert p == len(data)
    return rows


def strip_style(text: str) -> str:
    return STYLE.sub("", text).replace("\r\n", "\n")


def sentence_signature(text: str) -> str:
    # This groups textual numeric variants only; do not call this a mechanic ID.
    text = strip_style(text)
    text = PLACEHOLDER.sub("{VALUE}", text)
    text = re.sub(r"[-+]?\d+(?:[.,]\d+)*", "{N}", text)
    return re.sub(r"\s+", " ", text).strip()


def classify(row):
    text = strip_style(row["text"])
    tags = [key for key, _, regex in TAGS if regex.search(text)]
    placeholders = sorted(set(PLACEHOLDER.findall(text)))
    literal_percents = sorted(set(re.findall(r"(?<![\w#])\d+(?:\.\d+)?\s*%", text)))
    # A broad inclusion rule with an unfiltered escape hatch, not an ability count.
    candidate = bool(STATUS_KEY.match(row["key"]) or re.search(r"##skill[^#]*##", text) or
                     set(tags) & STRONG_TAGS or (tags and len(text) > 18 and TRIGGER.search(text)))
    context = "promotion-or-acquisition" if PROMO.search(text) else "status-ui" if STATUS_KEY.match(row["key"]) else "unresolved"
    if candidate and context == "unresolved" and (re.search(r"##skill[^#]*##", text) or (len(text) > 30 and TRIGGER.search(text))):
        context = "effect-description-candidate"
    return text, tags, placeholders, literal_percents, candidate, context


def linked_items(data: bytes, by_key):
    stride = 428
    if len(data) % stride:
        raise ValueError("ItemList length not divisible by observed 428-byte stride")
    rows = []
    for ordinal in range(len(data) // stride):
        offset = ordinal * stride
        b = data[offset:offset + stride]
        item_id = struct.unpack_from("<i", b)[0]
        # A few records store literal UTF-8 미정 instead of a lookup key.
        name_key = b[4:20].split(b"\0", 1)[0].decode("utf-8", errors="strict")
        description_key = b[20:36].split(b"\0", 1)[0].decode("utf-8", errors="strict")
        names = by_key.get(name_key, [])
        descriptions = by_key.get(description_key, [])
        rows.append({
            "itemId": item_id, "ordinal": ordinal, "recordOffset": offset,
            "nameKey": name_key, "descriptionKey": description_key,
            "type36Raw": struct.unpack_from("<i", b, 36)[0],
            "type40Raw": struct.unpack_from("<i", b, 40)[0],
            "nameRecords": [r["recordId"] for r in names],
            "descriptionRecords": [r["recordId"] for r in descriptions],
            "names": sorted(set(r["text"] for r in names)),
            "descriptions": sorted(set(r["text"] for r in descriptions)),
            "nameResolved": bool(names), "descriptionResolved": bool(descriptions),
        })
    if len({r["itemId"] for r in rows}) != len(rows):
        raise ValueError("ItemList IDs unexpectedly not unique")
    return rows


def write_html(output: Path, rows, items):
    # Data as separate JS allows file:// use without CORS fetches or a server.
    slim = [{"id": r["recordId"], "key": r["key"], "text": r["text"], "plain": r["plainText"],
             "tags": r["tags"], "candidate": r["abilityCandidate"], "context": r["contextCandidate"],
             "offset": r["textOffset"], "refs": r["referencedItemIds"],
             "nameRefs": r["nameReferencedItemIds"], "descRefs": r["descriptionReferencedItemIds"]} for r in rows]
    item_slim = [{"id": i["itemId"], "offset": i["recordOffset"], "nameKey": i["nameKey"], "descKey": i["descriptionKey"],
                  "names": i["names"], "descriptions": i["descriptions"], "nameRecords": i["nameRecords"],
                  "descRecords": i["descriptionRecords"], "nameResolved": i["nameResolved"], "descResolved": i["descriptionResolved"],
                  "tags": i["descriptionTags"], "effectCandidate": i["descriptionAbilityCandidate"],
                  "numericModifierCandidate": i["numericEffectModifierCandidate"], "type36": i["type36Raw"], "type40": i["type40Raw"]} for i in items]
    (output / "catalog-data.js").write_text("window.APK_TEXT_RECORDS=" + json.dumps(slim, ensure_ascii=False, separators=(",", ":")).replace("</script", "<\\/script") + ";\nwindow.APK_ITEM_RECORDS=" + json.dumps(item_slim, ensure_ascii=False, separators=(",", ":")).replace("</script", "<\\/script") + ";\n", encoding="utf-8")
    categories = "".join(f'<option value="{key}">{label}</option>' for key, label, _ in TAG_RULES)
    html = r'''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>모두의마블 APK 특수능력 원문 조사</title><style>
*{box-sizing:border-box}body{margin:0;background:#0d1724;color:#e5ebf3;font:15px/1.65 system-ui,"Malgun Gothic",sans-serif}header{background:#16253a;padding:28px max(24px,calc((100vw - 1280px)/2));border-bottom:1px solid #34445c}h1{margin:0;font-size:26px}h2{font-size:20px;margin:8px 0}p{margin:7px 0;color:#bac8d9}.controls,main{max-width:1280px;margin:auto;padding:20px 24px}.controls{position:sticky;top:0;background:#0d1724ee;backdrop-filter:blur(12px);z-index:2;display:grid;grid-template-columns:1fr 250px 260px;gap:12px}input,select,button{font:inherit;border:1px solid #4c6481;background:#1b2c43;color:#fff;border-radius:8px;padding:10px}button{cursor:pointer}.quick{display:flex;gap:8px;flex-wrap:wrap;grid-column:1/-1}.quick button{font-size:13px;padding:5px 10px}#summary{grid-column:1/-1;color:#a8c2df;font-size:13px}.row{padding:18px 0;border-bottom:1px solid #304259}.meta{color:#8ab9e9;font:13px/1.5 ui-monospace,monospace;display:flex;gap:14px;flex-wrap:wrap}.body{white-space:pre-wrap;margin:8px 0;color:#eef3f9;overflow-wrap:anywhere}.tags{display:flex;gap:7px;flex-wrap:wrap}.tag{font-size:12px;background:#233f5b;padding:2px 8px;border-radius:12px}.missing{background:#673e2d;color:#ffe2ab}.note{color:#f5ce81;font-size:13px}.refs{font-size:13px;color:#aac8e8;margin:6px 0}.refs button{font-size:12px;margin:3px;padding:3px 7px}.source{color:#8ca9c7;font-size:12px;overflow-wrap:anywhere}#more{margin:20px auto;display:block}.links{font-size:13px}a{color:#82c4ff}@media(max-width:850px){.controls{grid-template-columns:1fr}.controls>*{grid-column:1!important}h1{font-size:22px}}mark{background:#f4cc68;color:#142235}
</style><header><h1>모두의마블 특수능력 원문 조사</h1><p>제공된 APK 10.9.02의 ADD_ALL 전체 원문과 ItemList 이름·설명 참조를 검색합니다.</p><p>분류·후보 판정은 검색 보조입니다. 텍스트를 공급하는 자료이며 숨겨진 발동 코드·현재 서버 전체 도감·확률을 복원한 결과가 아닙니다.</p><p>아이템 연결은 파일에 저장된 이름·설명 키의 직접 참조만 뜻합니다. 해당 아이템이 그 특수능력을 보유한다는 추론은 하지 않았습니다.</p><div class="links"><a href="summary.md">범위·검증·한계</a> · <a href="../mechanics/effect-overview.md">효과별 조사</a> · <a href="../mechanics/bubble-and-interactions.md">비눗방울·상호작용 조사</a> · <a href="all-text-records.jsonl">전체 원문 JSONL</a> · <a href="ability-candidates.csv">능력 후보 CSV</a> · <a href="linked-item-records.jsonl">아이템 참조 JSONL</a></div></header>
<div class="controls"><input id="search" autofocus placeholder="속박 / 아이템 이름 / ID / 키 / 여러 단어 AND"><select id="category"><option value="">모든 효과 분류</option>__OPTIONS__</select><select id="scope"><option value="effects">효과 설명·상태 후보</option><option value="candidates">모든 능력 후보·홍보 포함</option><option value="all">전체 원문·이름·안내</option><option value="items">아이템 이름·설명 참조</option></select><div class="quick"><button id="bubble">물방울·비눗방울 바로 보기</button><button id="bind">속박·붙잡기</button><button id="blackhole">블랙홀</button><button id="itemmode">아이템·카드 이름 찾기</button><button id="reset">전체 초기화</button></div><div id="summary"></div></div><main><div id="results"></div><button id="more">다음 100개 보기</button></main><script src="catalog-data.js"></script><script>
const $=id=>document.getElementById(id), records=window.APK_TEXT_RECORDS, items=window.APK_ITEM_RECORDS, labels=__LABELS__, itemMap=new Map(items.map(i=>[i.id,i])),recordMap=new Map(records.map(r=>[r.id,r]));let matched=[],shown=0,timer;
function el(tag,text,cls){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n}
function itemName(i){return i.names.length?i.names.join(' / '):'이름 미해석 ['+i.nameKey+']'}
function jumpItem(id){$('scope').value='items';$('category').value='';$('search').value=String(id);search()}
function jumpText(key){$('scope').value='all';$('category').value='';$('search').value=key;search()}
function itemRefs(a,ids,title){if(!ids.length)return;const d=el('details',undefined,'refs');d.append(el('summary',title+' '+ids.length.toLocaleString()+'행 · 이름 보기 (능력 소유 연결 아님)'));for(const id of ids){const i=itemMap.get(id);if(!i)continue;const b=el('button',itemName(i)+' · ID '+id);b.addEventListener('click',()=>jumpItem(id));d.append(b)}a.append(d)}
function drawText(r){const a=el('article',undefined,'row'),meta=el('div',undefined,'meta');meta.append(el('span',r.id+' · '+r.key),el('span','ADD_ALL @'+r.offset),el('span',r.context));a.append(meta,el('div',r.plain,'body'));itemRefs(a,r.descRefs,'이 원문을 설명 키로 직접 참조');itemRefs(a,r.nameRefs,'이 원문을 이름 키로 직접 참조');if(r.text!==r.plain){const d=el('details'),s=el('summary','스타일 태그 포함 원문');d.append(s,el('div',r.text,'body'));a.append(d)}const tags=el('div',undefined,'tags');for(const t of r.tags)tags.append(el('span',labels[t]||t,'tag'));a.append(tags);return a}
function sourceTextLinks(a,ids,role){for(const id of ids){const r=recordMap.get(id);if(!r)continue;const b=el('button',role+' '+r.key+' · '+id+' @'+r.offset);b.addEventListener('click',()=>jumpText(r.key));a.append(b)}}
function drawItem(i){const a=el('article',undefined,'row'),meta=el('div',undefined,'meta');meta.append(el('span','ItemList ID '+i.id),el('span','ItemList.dat @'+i.offset),el('span','종류 원시값 '+i.type36+'/'+i.type40+' · 의미 미확정'));a.append(meta,el('h2',itemName(i)));const badges=el('div',undefined,'tags');if(!i.nameResolved)badges.append(el('span','이름 키 미해석','tag missing'));if(!i.descResolved)badges.append(el('span','설명 키 미해석','tag missing'));if(i.numericModifierCandidate)badges.append(el('span','수치 효과 문구 후보 · 소유 스킬 아님','tag'));a.append(badges);a.append(el('div','이름 키: '+i.nameKey+'  |  설명 키: '+i.descKey,'source'));if(i.descriptions.length)for(const text of i.descriptions)a.append(el('div',text,'body'));else a.append(el('div','이 APK의 ADD_ALL에 해당 설명 키가 없습니다. 다른 레코드로 대체하거나 추측하지 않았습니다.','note'));const links=el('div',undefined,'refs');sourceTextLinks(links,i.nameRecords,'이름 원문');sourceTextLinks(links,i.descRecords,'설명 원문');a.append(links);const tags=el('div',undefined,'tags');for(const t of i.tags)tags.append(el('span',labels[t]||t,'tag'));a.append(tags);return a}
function addPage(){const end=Math.min(shown+100,matched.length),frag=document.createDocumentFragment(),isItems=$('scope').value==='items';for(let i=shown;i<end;i++)frag.append(isItems?drawItem(matched[i]):drawText(matched[i]));$('results').append(frag);shown=end;$('more').hidden=shown>=matched.length;$('summary').textContent=`${isItems?'ItemList '+items.length.toLocaleString()+'행':'전체 '+records.length.toLocaleString()+'개 원문'} 중 검색 결과 ${matched.length.toLocaleString()}개 · ${shown.toLocaleString()}개 표시 · 원문·미치환 값 유지`}
const textHays=new Map(records.map(r=>[r.id,(r.id+' '+r.key+' '+r.plain+' '+r.refs.map(id=>{const i=itemMap.get(id);return i?id+' '+i.names.join(' '):String(id)}).join(' ')).toLowerCase()]));
const itemHays=new Map(items.map(i=>[i.id,(i.id+' '+i.nameKey+' '+i.descKey+' '+i.names.join(' ')+' '+i.descriptions.join(' ')).toLowerCase()]));
function search(){const words=$('search').value.trim().toLowerCase().split(/\s+/).filter(Boolean),cat=$('category').value,scope=$('scope').value;matched=scope==='items'?items.filter(i=>(!cat||i.tags.includes(cat))&&words.every(w=>itemHays.get(i.id).includes(w))):records.filter(r=>{if(scope==='effects'&&(!r.candidate||r.context==='promotion-or-acquisition'))return false;if(scope==='candidates'&&!r.candidate)return false;if(cat&&!r.tags.includes(cat))return false;return words.every(w=>textHays.get(r.id).includes(w))});shown=0;$('results').replaceChildren();addPage()}
function preset(category){$('scope').value='effects';$('search').value='';$('category').value=category;search()}
$('search').addEventListener('input',()=>{clearTimeout(timer);timer=setTimeout(search,180)});$('category').addEventListener('change',search);$('scope').addEventListener('change',search);$('more').addEventListener('click',addPage);$('bubble').addEventListener('click',()=>preset('bubble'));$('bind').addEventListener('click',()=>preset('bind'));$('blackhole').addEventListener('click',()=>preset('black-hole'));$('itemmode').addEventListener('click',()=>{ $('scope').value='items';$('category').value='';$('search').value='';search()});$('reset').addEventListener('click',()=>preset(''));if(location.hash==='#bubble')preset('bubble');else search();
</script></html>'''
    html = html.replace("__OPTIONS__", categories).replace("__LABELS__", json.dumps({k: v for k, v, _ in TAG_RULES}, ensure_ascii=False))
    (output / "index.html").write_text(html, encoding="utf-8")


def write_csv(path, fields, rows):
    # Protect spreadsheet readers from formula interpretation of source strings.
    def safe(value):
        if isinstance(value, (list, dict)):
            value = json.dumps(value, ensure_ascii=False, separators=(",", ":"))
        if isinstance(value, str) and value[:1] in ("=", "+", "-", "@"):
            return "'" + value
        return value
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(fields)
        for row in rows:
            writer.writerow([safe(row.get(field, "")) for field in fields])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", nargs="?", type=Path, default=DEFAULT_APK)
    parser.add_argument("--out", type=Path, default=OUT)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    digest = sha(args.apk.read_bytes())
    if digest != EXPECTED_SHA:
        raise ValueError("APK hash differs from inspected original. Do not silently mix catalogs.")
    with zipfile.ZipFile(args.apk) as z:
        data = z.read("assets/Data/ADD_ALL")
        item_data = z.read("assets/Data/ItemList.dat")
    rows = parse_add_all(data)
    by_key = defaultdict(list)
    for row in rows:
        by_key[row["key"]].append(row)
    items = linked_items(item_data, by_key)
    item_refs = defaultdict(set)
    name_refs = defaultdict(set)
    desc_refs = defaultdict(set)
    for item in items:
        for key in [item["nameKey"], item["descriptionKey"]]:
            item_refs[key].add(item["itemId"])
        name_refs[item["nameKey"]].add(item["itemId"])
        desc_refs[item["descriptionKey"]].add(item["itemId"])
    groups = defaultdict(list)
    tag_counts = Counter()
    context_counts = Counter()
    namespace_counts = Counter()
    for row in rows:
        plain, tags, placeholders, percents, candidate, context = classify(row)
        signature = sentence_signature(row["text"])
        row.update({"plainText": plain, "tags": tags, "placeholders": placeholders,
                    "literalPercentTokens": percents, "abilityCandidate": candidate,
                    "contextCandidate": context, "referencedItemIds": sorted(item_refs[row["key"]]),
                    "nameReferencedItemIds": sorted(name_refs[row["key"]]),
                    "descriptionReferencedItemIds": sorted(desc_refs[row["key"]])})
        if candidate:
            group_id = "SENT-" + sha(signature.encode("utf-8"))[:16]
            row["numericNormalizedSentenceGroup"] = group_id
            groups[group_id].append(row)
            tag_counts.update(tags)
            context_counts[context] += 1
        namespace = re.sub(r"\d+", "#", row["key"])
        namespace_counts[namespace] += 1
    candidates = [r for r in rows if r["abilityCandidate"]]
    effects = [r for r in candidates if r["contextCandidate"] != "promotion-or-acquisition"]
    for item in items:
        descriptions = by_key.get(item["descriptionKey"], [])
        item["descriptionTags"] = sorted({tag for r in descriptions for tag in r["tags"]})
        item["descriptionAbilityCandidate"] = any(r["abilityCandidate"] and r["contextCandidate"] != "promotion-or-acquisition" for r in descriptions)
        # A narrower textual class for numerical modifiers, not skill ownership.
        item["numericEffectModifierCandidate"] = any(
            r["contextCandidate"] != "promotion-or-acquisition" and bool(r["tags"]) and
            bool(re.search(r"\d+(?:\.\d+)?\s*%.*(?:증가|감소)|(?:증가|감소).*\d+(?:\.\d+)?\s*%", r["plainText"]))
            for r in descriptions)
    linked_effect_items = [item for item in items if item["descriptionAbilityCandidate"]]
    linked_modifier_items = [item for item in items if item["numericEffectModifierCandidate"]]
    duplicates = {key: [r["recordId"] for r in values] for key, values in by_key.items() if len(values) > 1}
    missing_name = sum(not item["nameResolved"] for item in items)
    missing_desc = sum(not item["descriptionResolved"] for item in items)
    manifest = {
        "scope": "User-supplied APK static resources only; not current live server catalog",
        "method": "Full sequential, strict UTF-8, round-trip-validated ADD_ALL parser; bounded fixed-stride ItemList reference reader",
        "apk": {"path": str(args.apk), "sha256": digest},
        "sources": [{"entry": "assets/Data/ADD_ALL", "bytes": len(data), "sha256": sha(data)},
                    {"entry": "assets/Data/ItemList.dat", "bytes": len(item_data), "sha256": sha(item_data)}],
        "coverage": {"addAllBytesParsed": len(data), "addAllTrailingBytes": 0, "addAllRoundTripVerifiedRecords": len(rows),
                     "allTextRecords": len(rows), "distinctKeys": len(by_key), "duplicateKeys": len(duplicates),
                     "uniqueExactTexts": len({r["text"] for r in rows}), "emptyTexts": sum(not r["text"] for r in rows),
                     "abilityCandidateRecords": len(candidates), "nonPromotionCandidateRecords": len(effects),
                     "numericNormalizedSentenceGroups": len(groups), "itemRows": len(items),
                     "itemNameLookupMissingRows": missing_name, "itemDescriptionLookupMissingRows": missing_desc,
                     "itemRowsBothLookupsResolved": sum(i["nameResolved"] and i["descriptionResolved"] for i in items),
                     "itemRowsWithBroadEffectDescriptionCandidate": len(linked_effect_items),
                     "linkedDescriptionKeysWithBroadEffectCandidate": len({i["descriptionKey"] for i in linked_effect_items}),
                     "linkedExactTextsWithBroadEffectCandidate": len({r["text"] for i in linked_effect_items for r in by_key[i["descriptionKey"]] if r["abilityCandidate"]}),
                     "itemRowsWithNumericModifierDescriptionCandidate": len(linked_modifier_items),
                     "linkedDescriptionKeysWithNumericModifierCandidate": len({i["descriptionKey"] for i in linked_modifier_items}),
                     "linkedItemDescriptionsWithSkillValuePlaceholder": sum(any("##skill" in r["text"] for r in by_key.get(i["descriptionKey"], [])) for i in items),
                     "candidateRowsWithUnresolvedSkillValues": sum(bool(re.search(r"##skill", r["text"])) for r in candidates)},
        "candidateTagCountsOverlapping": dict(tag_counts.most_common()),
        "candidateContextCounts": dict(context_counts),
        "namespaceCounts": dict(namespace_counts.most_common()),
        "limits": ["No game execution or network requests; packaged text does not prove runtime behavior or active availability.",
                   "Keywords are broad analyst search aids. Promotion/event/acquisition text can mention effects without defining them.",
                   "Numeric-normalized sentence groups are textual deduplication aids, not unique mechanic counts.",
                   "Literal percent tokens are not automatically activation probabilities; placeholders remain unresolved.",
                   "ItemList embedded keys are observed inventory name/description references only, NOT verified skill ownership. Categories/types and runtime carrier-to-skill links are not recovered.",
                   "Missing referenced text and dynamically downloaded/server data prevent a claim of all currently available abilities."]}
    save_json(out / "manifest.json", manifest)
    save_json(out / "duplicate-keys.json", duplicates)
    save_jsonl(out / "all-text-records.jsonl", rows)
    save_jsonl(out / "ability-candidates.jsonl", candidates)
    save_jsonl(out / "linked-item-records.jsonl", items)
    save_jsonl(out / "numeric-sentence-groups.jsonl", [{
        "groupId": key, "signature": sentence_signature(values[0]["text"]),
        "recordCount": len(values), "exactTextCount": len({r["text"] for r in values}),
        "recordIds": [r["recordId"] for r in values], "keys": [r["key"] for r in values],
        "tags": sorted({tag for r in values for tag in r["tags"]}),
    } for key, values in groups.items()])
    save_jsonl(out / "status-and-trap-text.jsonl", [r for r in rows if STATUS_KEY.match(r["key"])])
    save_jsonl(out / "bubble-related-records.jsonl", [r for r in rows if "bubble" in r["tags"] or "bind" in r["tags"]])
    write_csv(out / "ability-candidates.csv", ["recordId", "key", "textOffset", "text", "tags", "contextCandidate", "placeholders", "literalPercentTokens", "referencedItemIds", "numericNormalizedSentenceGroup"], candidates)
    write_csv(out / "linked-item-records.csv", ["itemId", "recordOffset", "nameKey", "descriptionKey", "names", "descriptions", "type36Raw", "type40Raw", "nameResolved", "descriptionResolved"], items)
    write_html(out, rows, items)
    counts = manifest["coverage"]
    summary = ["# 모두의마블 특수능력 APK 원문 카탈로그", "", "조사 대상: 사용자가 제공한 `모두의마블_10.9.02.apk`. 현재 서버 전체 능력 목록을 검증한 결과는 아닙니다.", "",
               "## 파싱 범위", "", f"- ADD_ALL **{len(data):,}바이트 전체**를 순차 파싱했습니다. 남은 바이트 0, 레코드 전체 재직렬화 일치.",
               f"- 전체 원문 **{len(rows):,}개**, 키 **{len(by_key):,}개**, 정확한 본문 종류 **{counts['uniqueExactTexts']:,}개**.",
               f"- 키워드 기반 능력 후보 **{len(candidates):,}개**. 홍보·획득 안내 후보를 제외하면 **{len(effects):,}개**. **능력 개수가 아니라 텍스트 레코드 개수**입니다.",
               f"- 수치만 정규화한 문장군 {len(groups):,}개. 이 역시 고유 효과 수가 아닙니다.",
               f"- ItemList 428바이트 단위 **{len(items):,}개**에서 이름·설명 키 참조를 읽었습니다. 두 참조가 모두 이 ADD_ALL에서 해석되는 항목은 {counts['itemRowsBothLookupsResolved']:,}개입니다.",
               f"- 이름 미해석 {missing_name:,}행, 설명 미해석 {missing_desc:,}행. 누락 자료 또는 버전별 다운로드 자료가 있을 수 있으며 원인은 아직 확정하지 않았습니다.",
               f"- 능력 후보 중 `##skill...##` 미치환 값이 남은 레코드 **{counts['candidateRowsWithUnresolvedSkillValues']:,}개**. 확률을 임의로 채우지 않았습니다.", "",
               "## 파일", "", "- `index.html`: 로컬에서 바로 열어 검색하는 전체 카탈로그. 효과 후보·전체 원문·아이템 이름/설명 참조 모드, 비눗방울 바로 보기 지원.",
               "- `all-text-records.jsonl`: 전체 원문과 스타일 필드·오프셋. 중복 키·빈 본문도 보존.",
               "- `ability-candidates.jsonl` / `.csv`: 검색 보조 후보·효과 태그·숫자 토큰·미치환 토큰.",
               "- `linked-item-records.jsonl` / `.csv`: ItemList에서 직접 관찰한 ID → 이름/설명 키 참조. 이를 해당 아이템이 그 능력을 소유한다는 연결로 해석하면 안 됩니다. 실제 스킬/발동 코드 연결은 미확인.",
               "- `status-and-trap-text.jsonl`: buff/trapinfo 명명 키의 전체 원문.",
               "- `bubble-related-records.jsonl`: 비눗방울·물방울·속박·붙잡기 관련 검색 결과. 이벤트 홍보가 섞일 수 있음.",
               "- `numeric-sentence-groups.jsonl`: 숫자 변형 검색용 문장군. 동작 동일성 보장 없음.",
               "- `manifest.json`: 소스 SHA-256, 커버리지, 분류 규칙의 한계 및 수량.", "", "## 효과 분류별 후보 레코드 수", "",
               "한 문장이 여러 분류에 들어가므로 합산하면 전체 수와 다릅니다.", "", "| 분류 | 레코드 수 |", "|---|---:|"]
    labels = {key: label for key, label, _ in TAG_RULES}
    summary.extend(f"| {labels[key]} | {count:,} |" for key, count in tag_counts.most_common())
    summary += ["", "## 아이템 참조와 능력 소유의 차이", "",
                f"ItemList 설명 키에 직접 연결되는 원문 중 넓은 효과 후보와 맞는 것은 **{len(linked_effect_items):,}행 / {counts['linkedDescriptionKeysWithBroadEffectCandidate']:,}개 설명 키 / {counts['linkedExactTextsWithBroadEffectCandidate']:,}개 정확한 본문**입니다. 꾸미기 주사위 소개·재료 안내 등 과잉 포함이 있어 실제 능력 수로 셀 수 없습니다.", "",
                f"수치와 증가·감소를 함께 명시한 설명은 **{len(linked_modifier_items):,}행 / {counts['linkedDescriptionKeysWithNumericModifierCandidate']:,}개 설명 키**입니다. 시간제 버프 등으로 보이는 텍스트이며 고유 캐릭터·행템 스킬 소유 관계를 입증하지 않습니다.", "",
                f"ItemList의 설명 참조 중 `##skill...##`가 든 원문에 연결된 행은 **{counts['linkedItemDescriptionsWithSkillValuePlaceholder']:,}개**입니다. 대부분 특수능력 텍스트를 아이템 이름에 직접 연결할 근거가 이 테이블에는 없습니다.", "",
                "검색 화면에서 원문을 이름/설명으로 직접 참조하는 항목의 실제 이름과 ID를 확인할 수 있습니다. 동일 키의 다른 문구는 덮어쓰지 않고 원문 ID별로 남겼으며, 이름 미해석 항목은 키를 그대로 표시합니다. 인접한 키나 홍보 문장의 위치만으로 캐릭터와 능력을 묶지 않았습니다.", "",
                "## 검증 경계", "", "원문에는 과거 버전, 이벤트·상점 홍보, 사용 중단된 효과, 도감 도움말이 함께 들어갈 수 있습니다. 화면에서 실제 장착할 수 있거나 현재 서버에서 발동한다는 의미가 아닙니다. `##skillvalue##` 및 스타일 태그를 보존했으며, 숫자만 있는 문구도 발동 확률로 단정하지 않았습니다.", "", "키워드 분류는 검색 보조이므로 누락·과잉 포함이 가능합니다. 이를 보완하려고 전체 원문을 필터 없이 함께 제공합니다. 이 자료는 표시할 텍스트를 공급하며 숨겨진 게임 엔진 동작을 복원한 결과가 아닙니다. 효과의 조건·해제·우선순위는 화면, 실제 게임 코드 및 실행 관찰의 후속 대조가 필요합니다.", "", "재현: `python Tools/extract_abilities.py [원본 APK 경로]`. 원본 해시가 다르면 중단하며 다운로드나 APK 실행은 하지 않습니다."]
    (out / "summary.md").write_text("\n".join(summary) + "\n", encoding="utf-8")
    print(json.dumps(manifest["coverage"], ensure_ascii=True))


if __name__ == "__main__":
    main()
