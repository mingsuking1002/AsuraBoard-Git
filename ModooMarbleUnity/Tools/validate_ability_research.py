"""Check exported research against exact supplied APK bytes; no game execution."""
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
RESEARCH = ROOT / 'Documentation/abilities'
CATALOG = RESEARCH / 'apk-catalog'
APK = Path(r'C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk')


def main():
    manifest = json.loads((CATALOG / 'manifest.json').read_text(encoding='utf8'))
    actual_apk_sha = hashlib.sha256(APK.read_bytes()).hexdigest()
    assert actual_apk_sha == manifest['apk']['sha256'], 'APK identity mismatch'
    with zipfile.ZipFile(APK) as apk:
        source = apk.read('assets/Data/ADD_ALL')
        assert len(source) == manifest['coverage']['addAllBytesParsed']
        rows = [json.loads(line) for line in (CATALOG / 'all-text-records.jsonl').open(encoding='utf8')]
        by_id = {}
        by_key_offset = {}
        end = 0
        for i, row in enumerate(rows):
            assert row['ordinal'] == i and row['recordOffset'] == end
            assert row['recordId'] not in by_id
            assert row['key'].encode('utf8') == source[row['keyOffset']:row['keyOffset'] + row['keyBytes']]
            assert row['text'].encode('utf8') == source[row['textOffset']:row['textOffset'] + row['textBytes']]
            end = row['recordEndOffset']
            by_id[row['recordId']] = row
            by_key_offset[row['key'], row['textOffset']] = row
        assert end == len(source), 'Trailing bytes or partial source coverage'
        assert len(rows) == manifest['coverage']['allTextRecords'] == 114847

        items = [json.loads(line) for line in (CATALOG / 'linked-item-records.jsonl').open(encoding='utf8')]
        assert len(items) == manifest['coverage']['itemRows'] == 29746
        assert len(apk.read('assets/Data/ItemList.dat')) == len(items) * 428
        for item in items:
            for field, key_field in [('nameRecords', 'nameKey'), ('descriptionRecords', 'descriptionKey')]:
                for record_id in item[field]:
                    assert by_id[record_id]['key'] == item[key_field]
        evidence_count = 0
        for line in (RESEARCH / 'mechanics/mechanic-evidence.jsonl').open(encoding='utf8'):
            row = json.loads(line)
            original = by_key_offset[row['key'], row['textOffset']]
            assert original['text'] == row['text']
            evidence_count += 1
        assert evidence_count == 9170
        assert len(apk.read('assets/Data/SkillBuff.dat')) == 285 * 68
        overview = json.loads((RESEARCH / 'mechanics/effect-overview.json').read_text(encoding='utf8'))
        assert overview['categoryCount'] == len(overview['effects']) == 38
        overview_sources = 0
        for effect in overview['effects']:
            for citation in effect['sources']:
                source_row = by_id[citation['recordId']]
                assert source_row['key'] == citation['key']
                assert source_row['textOffset'] == citation['textOffset']
                assert source_row['text'] == citation['text']
                overview_sources += 1

    checked_links = []
    for path in [RESEARCH / 'README.md', RESEARCH / 'apk-catalog/index.html']:
        text = path.read_text(encoding='utf8')
        links = re.findall(r'\]\(([^)]+)\)', text) if path.suffix == '.md' else re.findall(r'href="([^"]+)"', text)
        for link in links:
            if re.match(r'^(https?:|#|javascript:)', link):
                continue
            target = (path.parent / link.split('#', 1)[0]).resolve()
            assert target.is_file(), f'Broken research link: {path.name}: {link}'
            checked_links.append(str(target.relative_to(ROOT)))

    result = {
        'status': 'passed', 'dateKst': '2026-10-25',
        'apkSha256': actual_apk_sha,
        'sourceBytesCovered': len(source), 'textRecordsVerifiedAgainstBytes': len(rows),
        'itemRowsVerified': len(items), 'mechanicEvidenceRowsCrosschecked': evidence_count,
        'curatedEffectCategories': len(overview['effects']), 'curatedSourceReferencesVerified': overview_sources,
        'skillBuffRecordCount': 285, 'checkedLocalLinks': checked_links,
        'gameRuntimeObserved': False, 'accountLoginPerformed': False,
        'limitations': 'Verifies exported source evidence and links, not keyword completeness, live availability, hidden probabilities, or effect execution.'
    }
    destination = ROOT / 'Validation/abilities-audit.json'
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({k:v for k,v in result.items() if k != 'checkedLocalLinks'}, ensure_ascii=True))


if __name__ == '__main__':
    main()
