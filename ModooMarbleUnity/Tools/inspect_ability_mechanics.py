"""Execution-free mechanics evidence and raw table shape inspection.

This deliberately does not assign undocumented numeric fields to gameplay
semantics. ADD_ALL is parsed to exact EOF using its length-prefixed schema;
ItemList/SkillBuff fixed record shapes are validated and preserved separately.
"""
from __future__ import annotations

from pathlib import Path
from collections import Counter, defaultdict
import csv
import hashlib
import json
import re
import struct
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
APK = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk")
OUT = ROOT / "Documentation/abilities/mechanics"
EXPECTED_SHA = "2850a586f6b0f7109e87ed040ace7f1e1af7da9c9fa247f483708578f9b084bc"

# Taxonomy is an analyst index of literal first-party text, not a verified game enum.
FAMILIES = [
    ("bubble_binding", "비눗방울·속박", r"비눗방울|속박", "1회 이동 불가 및 랜드마크 속박은 출처 문맥별로 구분"),
    ("pull", "끌어당김", r"끌어당|끌어 당|당기고|당긴", "앞뒤 거리·같은 라인·건설/도착 발동은 각 변형별 확인"),
    ("hold", "붙잡기·경유 정지", r"붙잡|붙들|잡아두|검문소", "도착과 지나감의 발동 차이 보존"),
    ("push", "밀어내기·날려보내기", r"밀어내|밀쳐|날려 ?보|날려버", "목적지·동반 효과는 개별 문구 확인"),
    ("teleport", "선택·순간 이동", r"순간 ?이동|선택 ?이동|선택하여 이동|선택해 이동", "이동 중 경유 처리·월급은 미확인"),
    ("forced_movement", "강제 이동", r"강제 ?이동|강제로 이동", "다른 효과의 원자 동작일 수 있음"),
    ("travel", "세계여행·비행", r"세계여행|세계 여행|비행 이동", "기본 세계여행 문구도 포함하므로 특수능력 수와 동일하지 않음"),
    ("reverse", "역주행·후진", r"역주행|거꾸로 이동|뒤로 ?[0-9]+칸|-[0-9]+칸 이동", "이동 방향과 도착 효과 순서는 미확인"),
    ("swap", "위치·땅 교환", r"위치.{0,8}(?:교환|바꾸)|(?:교환|바꾸).{0,8}위치|땅.{0,8}교환|지역.{0,8}교환", "대상 제한을 개별 문구에 유지"),
    ("dice_control", "주사위 컨트롤", r"주사위 ?컨트롤|주컨|컨트롤 정확도", "성공 확률과 정확도 향상은 합치지 않음"),
    ("dice_cancel", "주사위 취소", r"주사위.{0,5}취소", "이미 굴린 결과를 취소하는 시점은 별도 확인 필요"),
    ("dice_scale", "주사위 배수·추가 눈금", r"주사위.{0,8}[234]배|주사위 눈금|이동 칸 수.{0,3}배", "더블 미적용 등 예외를 원문에 보존"),
    ("extra_roll", "주사위 재굴림·추가 턴", r"주사위.{0,8}(?:한 ?번 ?더|다시|추가)|추가 ?턴|한 ?턴 ?더", "횟수 제한·무한루프 방지 판정 미확인"),
    ("turn_end", "턴 종료·강제 종료", r"턴.{0,8}(?:강제.?종료|즉시.?종료)|강제.{0,5}턴.{0,5}종료|턴 종료", "일반 시스템 문구도 포함"),
    ("dice_curse", "주사위 저주·게이지 교란", r"주사위 저주|게이지.{0,8}(?:속도|연막)|게이지 조절 속도", "주사위 눈 자체와 조작 게이지 효과를 분리"),
    ("defense", "공격 방어·쉴드·보호막", r"공격 방어|방어 드론|쉴드|실드|보호막", "단일 방어 만능 처리 금지; 방어 가능 공격군별 확인"),
    ("reflect", "공격 반사", r"공격.{0,45}반사", "확률 자리표시자는 수치로 확정하지 않음"),
    ("ignore_defense", "방어·면역 무시", r"(?:방어|보호막|쉴드|실드|방어력).{0,20}무시", "무시와 방어는 별도 판정 단계 후보"),
    ("debuff_cleanse", "디버프 해제·즉시 탈출", r"디버프.{0,10}해제|비눗방울 속박 시.{0,75}해제|속박.{0,60}즉시 해제", "해제 확률·횟수 제한별 변형 존재"),
    ("skill_disable", "능력 무력화·허수아비", r"모든 스킬.{0,20}사용할 수 없|허수아비|능력.{0,15}무력화|스킬.{0,8}무력화", "전투 상태와 이벤트 교환 소재를 혼동하지 않음"),
    ("angel_nullification", "천사카드 무력화", r"천사카드.{0,80}무력화|천사 카드.{0,80}무력화", "통행료 면제·반사와 별도"),
    ("paint", "페인트·임시 소유권", r"페인트", "지속 턴과 독점 판정 시점을 별도 확인"),
    ("blackhole", "블랙홀·화이트홀", r"블랙홀|화이트홀|블랙 홀|화이트 홀", "생성·연결·경유·배수 효과는 변형별 구분"),
    ("magnetic_field", "전자기장", r"전자기장", "같은 라인 등 범위 확인"),
    ("bomb", "폭탄 설치·폭발", r"폭탄|폭발", "맵 규칙·미니게임·행운아이템이 혼재"),
    ("trap", "함정·장애물 설치", r"함정|나무덩쿨|늪지대|징검다리|빙판길|설치물", "맵 전용 설치물도 포함"),
    ("steal_marble", "마블 강탈·자금 이전", r"마블.{0,15}강탈|보유.{0,8}마블.{0,12}(?:가져|빼앗|빼앗아)|마블.{0,8}훔", "금액·비율·피해자별 제한 보존"),
    ("steal_land", "땅·건물 강탈", r"(?:땅|건물|지역|랜드마크).{0,15}(?:강탈|빼앗|뺏)|(?:빼앗|뺏).{0,15}(?:땅|건물|지역|랜드마크)", "인수와 무상/강제 강탈은 다름"),
    ("takeover", "인수·강제 인수", r"인수", "일반 규칙 문구도 포함"),
    ("auto_build", "자동·원격 건설", r"자동 ?건설|즉시 ?건설|원격 ?건설|랜드마크로.{0,8}(?:건설|만들)|건설.{0,8}랜드마크", "직접 건설 조건과 자동 건설 조건을 분리"),
    ("destroy", "건물 파괴·붕괴", r"건물.{0,12}(?:파괴|붕괴|철거)|(?:파괴|붕괴|철거).{0,12}건물|랜드마크.{0,12}파괴", "맵 붕괴 규칙도 포함"),
    ("toll_discount", "통행료 할인·면제", r"통행료.{0,30}(?:할인|면제)|통행료 면제", "기본통행료·최종통행료 기준은 확정 금지"),
    ("toll_scale", "통행료 배수·증폭", r"통행료.{0,18}(?:배|증가|상승|증폭)", "중첩 순서·상한은 별도 검증"),
    ("multiplier_transfer", "배수 이전·잠금", r"배수.{0,8}(?:이전|이동|고정)|배수이전|배수 이전 불가", "상대/내 땅 방향과 이전 불가를 분리"),
    ("double_toll", "추가 통행료 지불", r"통행료.{0,20}한 ?번 ?더|한 ?번 ?더.{0,10}지불", "속박 해제 후 재정산과 즉시 증폭은 다름"),
    ("olympics", "올림픽·축제", r"올림픽.{0,8}(?:개최|선택|이동|도착)|축제", "기본 맵과 특수 스킬이 혼재"),
    ("growth", "확률·효과 성장", r"성장.{0,8}\+|\+[0-9]+%.{0,5}성장|미발동.{0,20}성장|발동.{0,20}초기화", "미발동·홀짝 사용·턴 경과 등 성장 조건 구분"),
    ("copy", "복사·스킬 획득", r"스킬.{0,20}복사|능력.{0,20}복사|행운아이템.{0,20}복사", "분신 교환 소재와 구별"),
    ("ice_prison", "얼음 감옥", r"얼음 감옥", "겨울맵 무인도 대체 칸의 문맥; 캐릭터 빙결과 동일시 금지"),
    ("poison_candidate", "중독·독성 후보", r"중독|독성|독 데미지|독 대미지", "단어 존재만 확인; 맹독 화살촉의 설정 설명은 효과 증거 아님"),
]


def write_json(name, obj):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT/name).write_text(json.dumps(obj, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")


def add_records(data):
    offset = 0
    records = []

    def string():
        nonlocal offset
        if offset+4>len(data): raise ValueError(f"LP length outside file: {offset}")
        length = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        start=offset
        if length>1048576 or offset+length>len(data): raise ValueError(f"Invalid LP bounds at {start}")
        text=data[offset:offset+length].decode("utf-8", errors="strict")
        offset+=length
        return text,start,length

    while offset<len(data):
        start=offset
        key,_,_=string()
        text,text_offset,text_bytes=string()
        font,_,_=string()
        if offset+4>len(data): raise ValueError("Missing font size")
        size=struct.unpack_from("<i",data,offset)[0]
        offset+=4
        format_fields=[string()[0] for _ in range(5)]
        records.append({"key":key,"text":text,"recordOffset":start,"textOffset":text_offset,"textBytes":text_bytes,"endOffset":offset})
    assert offset==len(data)
    return records


def plain(text):
    return re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", text)).strip()


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    digest=hashlib.sha256(APK.read_bytes()).hexdigest()
    if digest!=EXPECTED_SHA: raise ValueError("Unexpected APK identity; choose a new evidence scope before analyzing")
    manifest={"apkPath":str(APK),"apkSha256":digest,"method":"Python zipfile/struct; exact static bytes; no target execution/network", "sources":[]}
    with ZipFile(APK) as z:
        source={}
        for entry in ["assets/Data/ADD_ALL","assets/Data/SkillBuff.dat","assets/Data/ItemList.dat"]:
            b=z.read(entry)
            source[entry]=b
            manifest["sources"].append({"entry":entry,"bytes":len(b),"sha256":hashlib.sha256(b).hexdigest()})
    records=add_records(source["assets/Data/ADD_ALL"])
    by_key=defaultdict(list)
    for r in records: by_key[r["key"]].append(r)
    taxonomy=[]
    evidence={}
    for family,label,pattern,note in FAMILIES:
        rx=re.compile(pattern,re.DOTALL)
        hits=[r for r in records if rx.search(r["text"])]
        # Each full source occurrence remains available, including duplicates.
        for r in hits: evidence[(r["recordOffset"],r["key"])]=r
        representatives=sorted(hits,key=lambda r:("조건" not in r["text"],"효과" not in r["text"],len(r["text"])<25,len(r["text"])))[:12]
        taxonomy.append({"id":family,"label":label,"literalPattern":pattern,"note":note,"matchingRecordCount":len(hits),
                         "uniquePlainTextCount":len(set(plain(r["text"]) for r in hits)),
                         "references":[{"key":r["key"],"recordOffset":r["recordOffset"],"textOffset":r["textOffset"]} for r in hits],
                         "examples":representatives})
    fixed_status=[r for r in records if re.fullmatch(r"(?:buff_(?:title|text)|trapinfo_(?:title|desc))_\d+",r["key"])]
    bubble=[r for r in records if re.search("비눗방울|속박|물방울",r["text"])]
    for r in fixed_status+bubble:evidence[(r["recordOffset"],r["key"])]=r
    write_json("mechanic-taxonomy.json",{"classification":"Analyst literal-text index, not a recovered engine enum; match counts include names/ads/tutorials and are not ability counts", "sourceRecordCount":len(records),"families":taxonomy})
    write_json("status-and-trap-guide.json",{"source":"assets/Data/ADD_ALL","observedRecords":fixed_status,"limit":"Guide labels are static claims, not execution evidence or verified enum mapping"})
    guide=["# 이름이 명시된 버프·함정 가이드 전수", "", "제공된 APK `ADD_ALL`에서 `buff_title_N`, `buff_text_N`, `trapinfo_title_N`, `trapinfo_desc_N`에 해당하는 60개 레코드를 전부 모았다. 아래 번호는 **문자열 키의 접미사**이며 엔진의 실제 enum이라고 확정하지 않는다.", ""]
    for prefix,title,desc in [("buff","버프","text"),("trapinfo","설치물·미니게임 효과","desc")]:
        guide += ["## "+title,"","|키 번호|이름|설명|설명 텍스트 오프셋|","|---:|---|---|---:|"]
        numbers=sorted({int(r['key'].rsplit('_',1)[1]) for r in fixed_status if r['key'].startswith(prefix+'_title_')})
        for n in numbers:
            titles=by_key[prefix+'_title_'+str(n)]
            descriptions=by_key.get(prefix+'_'+desc+'_'+str(n),[])
            for r in descriptions:
                guide.append(f"|{n}|{titles[0]['text']}|{r['text'].replace(chr(10),'<br>').replace('|','&#124;')}|{r['textOffset']:,}|")
    guide += ["", "같은 효과로 보이는 별도 키도 원문대로 보존했다. 설명이 같다고 실제 엔진 ID와 처리까지 같다는 뜻은 아니다."]
    (OUT/"status-and-trap-guide.md").write_text("\n".join(guide)+"\n",encoding="utf-8")
    write_json("bubble-all-contexts.json",{"source":"assets/Data/ADD_ALL","records":bubble,"limit":"Contains ability binding, minigame traps, map bubbles and unrelated flavor text; separate by key and context"})
    with (OUT/"mechanic-evidence.jsonl").open("w",encoding="utf-8") as f:
        for _,r in sorted(evidence.items()):f.write(json.dumps(r,ensure_ascii=False)+"\n")

    data=source["assets/Data/ItemList.dat"]
    assert len(data)%428==0
    ids=[]; types=Counter(); type_examples=defaultdict(list); unresolved=Counter(); literal_names=[]
    for index,offset in enumerate(range(0,len(data),428)):
        row=data[offset:offset+428]
        id=struct.unpack_from("<I",row)[0];ids.append(id)
        name=row[4:20].split(b"\0")[0].decode("utf-8",errors="strict")
        desc=row[20:36].split(b"\0")[0].decode("utf-8",errors="strict")
        pair=struct.unpack_from("<2i",row,36); types[pair]+=1
        if name not in by_key:unresolved["name"]+=1
        if desc not in by_key:unresolved["description"]+=1
        if not name.isascii(): literal_names.append({"index":index,"recordOffset":offset,"id":id,"nameField":name})
        if len(type_examples[pair])<8:
            type_examples[pair].append({"index":index,"recordOffset":offset,"id":id,"nameField":name,
                                        "nameCandidates":by_key.get(name,[]),"descriptionField":desc,"descriptionCandidates":by_key.get(desc,[])})
    assert len(ids)==len(set(ids))
    write_json("itemlist-shape.json",{"entry":"assets/Data/ItemList.dat","recordStride":428,"recordCount":len(ids),"uniqueIdCount":len(set(ids)),
              "observedLayout":[{"offset":0,"bytes":4,"type":"uint32","meaning":"unique numeric ID candidate"},
                                {"offset":4,"bytes":16,"type":"NUL padded UTF-8","meaning":"name key or literal name"},
                                {"offset":20,"bytes":16,"type":"NUL padded UTF-8","meaning":"description key"},
                                {"offset":36,"bytes":4,"type":"int32","meaning":"unknown category candidate"},
                                {"offset":40,"bytes":4,"type":"int32","meaning":"unknown subtype candidate"}],
              "unresolvedInPackagedAddAll":dict(unresolved),"literalNames":literal_names,
              "typePairs":[{"raw36":p[0],"raw40":p[1],"count":n,"examples":type_examples[p]}for p,n in types.most_common()],
              "limitations":["Field roles inferred from valid labels and repetition, not decompiled reader code", "Other numeric fields intentionally not assigned gameplay semantics", "All ItemList rows are not all abilities; store/currency/packs/materials included", "A missing key in packaged text does not prove missing current-game content"]})
    data=source["assets/Data/SkillBuff.dat"]
    assert len(data)%68==0
    rows=[]
    for index,offset in enumerate(range(0,len(data),68)):
        row=data[offset:offset+68]
        a,b,c=struct.unpack_from("<3I",row)
        refs=[]
        for id in set([a,b]):
            for prefix in ["buff_title_","buff_text_"]:
                refs.extend(by_key.get(prefix+str(id),[]))
        rows.append({"index":index,"recordOffset":offset,"rawId0":a,"raw4":b,"raw8":c,"rawByte12":row[12],
                     "rawI32At13_17_21_25":list(struct.unpack_from("<4i",row,13)),"rawByte29":row[29],
                     "rawI32At30_34_38_42_46_50":list(struct.unpack_from("<6i",row,30)),"rawByte54":row[54],"rawByte55":row[55],
                     "rawI32At56_60_64":list(struct.unpack_from("<3i",row,56)),"rawHex":row.hex(),
                     "candidateSameNumericLabelReferences":refs})
    write_json("skillbuff-raw-records.json",{"entry":"assets/Data/SkillBuff.dat","recordStride":68,"recordCount":len(rows),
              "distinctRawId0":len(set(r["rawId0"] for r in rows)),"records":rows,
              "limitations":["Stride inferred from whole file division and regular numeric field alignment", "rawId0 repeats; distinct row count is not distinct ability count", "Same numeric label match is a candidate association only", "Unaligned field widths fit observed packed records, but field types and semantics are not code verified", "Numeric values must not be interpreted as probabilities/durations without corroboration"]})
    manifest.update({"addAllRecords":len(records),"addAllBytesConsumed":len(source["assets/Data/ADD_ALL"]),"mechanicFamilyCount":len(taxonomy),"mechanicEvidenceRecords":len(evidence),"statusGuideRecords":len(fixed_status),"bubbleContextRecords":len(bubble),"itemListRows":len(ids),"skillBuffRows":len(rows)})
    write_json("source-manifest.json",manifest)
    lines=["# 특수능력 효과별 조사 근거", "", "범위: 사용자가 제공한 APK 10.9.02의 정적 자료. 실행 장면이나 최신 서버 데이터 전체를 확인한 결과가 아니다.","", 
           f"ADD_ALL을 길이 접두사 구조로 끝까지 읽어 **{len(records):,}개 레코드**를 확인했다. 효과 검색은 이 전체 레코드에서 수행했다.","", 
           "분류별 건수는 검색에 걸린 원문 레코드 수다. 이름·효과·강화 안내·이벤트 광고·중복 변형이 섞여 있으므로 고유 능력 개수가 아니다.","", "|분류|원문 레코드|중복 제거 문장|범위 주의|","|---|---:|---:|---|"]
    lines += [f"|{t['label']}|{t['matchingRecordCount']:,}|{t['uniquePlainTextCount']:,}|{t['note']}|"for t in taxonomy]
    lines += ["", "## 파일", "", "- `mechanic-taxonomy.json`: 각 효과 분류의 검색식·전체 원문 참조·대표 문장", "- `mechanic-evidence.jsonl`: 분류와 관련된 전체 원문 및 바이트 오프셋", "- `status-and-trap-guide.json`: 이름이 명시된 버프·함정 가이드 전부", "- `bubble-all-contexts.json`: 비눗방울·속박·물방울 전체 문맥", "- `itemlist-shape.json`: ItemList의428바이트×29,746행 구조·키 연결·미해결 키 수", "- `skillbuff-raw-records.json`: SkillBuff의68바이트×285행 원문과 숫자; 의미 추정 금지", "- `source-manifest.json`: APK와 각 소스의 SHA-256, 처리 범위", "", "## 확인 한계", "", "특수능력의 실제 실행 순서, 발동 확률의 최종 수치, 캐릭터/행템의 현재 장착 가능 여부, 시즌별 버전 차이는 이 자료만으로 확정할 수 없다. `##skillvalue##` 등 자리표시자는 숫자로 대체하지 않았다. 값이 명시된 문구도 그 문구의 특정 강화·변형에만 해당한다.", "", "`물방울`에는 금성의 황산 물방울 설정과 맵 기믹이 섞인다. 플레이어를 가두는 효과의 실제 표기는 `비눗방울 속박`이며, 붙잡기·감옥·허수아비와 한 효과로 합치면 안 된다."]
    (OUT/"taxonomy.md").write_text("\n".join(lines)+"\n",encoding="utf-8")
    print(json.dumps(manifest,ensure_ascii=True))


if __name__=="__main__":main()
