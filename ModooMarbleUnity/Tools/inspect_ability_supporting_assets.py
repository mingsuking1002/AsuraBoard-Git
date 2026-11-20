"""Read-only metadata inspection of explicitly selected ability-adjacent APK assets.

No sprites/pixels are decoded. Scene identifiers are existence evidence only.
Numeric table schema and filename meaning remain unverified.
"""
from pathlib import Path
from zipfile import ZipFile
import hashlib,json,re,struct,zlib

ROOT=Path(__file__).resolve().parents[1]
APK=Path(r'C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk')
OUT=ROOT/'Documentation/abilities/mechanics'
TABLES=['BaseCampDice','BombBattleValue','BombMiniGameChar','BomberAttack','BossAtt','BossDice','BossSkill','CraneProbability',
        'DiceEffectInfo','FlyingItemInfo','SeedCfg','SeedOption','StructAttack','StructChargeRecovery','UserAtt','UserAttDomi','ZombiAttackMulti']
SPRITES=['GameEffectBoardLuckyitem','GameEffectBoardLuckyitem_v79','GameEffectDebuff_new']


def scene_inventory(data):
    if data[:8]!=b'F3ZT_SPR':raise ValueError('Unsupported source container')
    expected=struct.unpack_from('<Q',data,20)[0]
    blob=data[40:]if data[40:42]==b'N2'else zlib.decompress(data[40:])
    assert blob[:2]==b'N2'and(not expected or len(blob)==expected)
    start=struct.unpack_from('<I',blob,4)[0]+12
    chunks={}
    for tag in [204,207]:
        offset=blob.find(b'N2'+bytes([tag,0]),start)
        if offset<0:continue
        count,stride=struct.unpack_from('<II',blob,offset+4)
        assert 32<=stride<2048 and 0<=count<100000 and offset+12+count*stride<=len(blob)
        records=[]
        for index in range(count):
            rstart=offset+12+index*stride
            raw=blob[rstart:rstart+32].split(b'\0')[0]
            try:
                name=raw.decode('utf8',errors='strict');encoding='utf8'
            except UnicodeDecodeError:
                try:
                    name=raw.decode('cp949',errors='strict');encoding='cp949'
                except UnicodeDecodeError:
                    name=raw.decode('utf8',errors='backslashreplace');encoding='unresolved or truncated fixed32-byte UTF8; invalid bytes escaped'
            records.append({'index':index,'decodedOffset':rstart,'name':name,'nameEncoding':encoding,'nameBytesHex':raw.hex()})
        chunks[str(tag)]={'count':count,'stride':stride,'decodedChunkOffset':offset,'records':records}
    return {'declaredDecodedBytes':expected,'observedDecodedBytes':len(blob),'decodedSha256':hashlib.sha256(blob).hexdigest(),
            'sceneChunk204':chunks.get('204'),'multiSceneChunk207':chunks.get('207')}


def main():
    digest=hashlib.sha256(APK.read_bytes()).hexdigest()
    assert digest=='2850a586f6b0f7109e87ed040ace7f1e1af7da9c9fa247f483708578f9b084bc'
    output={'apkSha256':digest,'method':'zipfile/struct/zlib only; no APK execution; no pixel decoding','tables':[],'visualEffectResources':[]}
    with ZipFile(APK)as z:
        for name in TABLES:
            entry='assets/Data/'+name+'.dat';data=z.read(entry)
            strings=[{'offset':m.start(),'value':m.group().decode('ascii')}for m in re.finditer(rb'[A-Za-z][A-Za-z0-9_./-]{3,}',data)]
            output['tables'].append({'entry':entry,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),
                 'first64BytesHex':data[:64].hex(),'asciiRunCount':len(strings),'firstAsciiRuns':strings[:30],
                 'strideCandidates': [{'stride':s,'rows':len(data)//s}for s in range(12,1025)if len(data)%s==0],
                 'limitation':'Stride divisibility and ASCII runs are raw observations only; names do not establish field meaning or gameplay probabilities'})
        for name in SPRITES:
            entry='assets/spr/'+name+'.f3spr';data=z.read(entry)
            output['visualEffectResources'].append({'entry':entry,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),**scene_inventory(data),
                 'limitation':'Recovered scene identifiers show packaged visual resources; do not prove ability behavior, current availability, or numeric skill-to-scene mapping'})
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'supporting-assets.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    lines=['# 능력 관련 추가 테이블·연출 리소스 조사','',
           '선택한 APK 자산을 정적으로 읽었다. 표 이름과 장면 이름은 존재 증거이며 **수치의 의미나 실제 스킬 구현을 복원한 것은 아니다**. 이미지 픽셀은 디코딩하지 않았다.','',
           '## 추가 테이블','',
           '기존 ADD_ALL·SkillBuff·ItemList 이외에 다음 파일이 존재한다. 본 조사에서는 바이트 길이·SHA-256·일부 ASCII 문자열·가능한 레코드 길이를 기록했다. 나눗셈이 맞는 레코드 길이는 후보일 뿐이다.','',
           '|파일|바이트|현재 판단|','|---|---:|---|']
    for t in output['tables']:
        lines.append('|`'+t['entry']+'`|'+f"{t['bytes']:,}"+'|필드 의미 미해석; 일반 캐릭터·행템 확률표로 단정 금지|')
    lines+=['','`BossSkill`, `BossAtt`, `BossDice` 등은 이름상 보스 모드 관련 후보이나 내용 의미는 미확정이다. `CraneProbability`를 일반 능력 발동률로 쓰거나 `UserAtt`를 최신 캐릭터 스킬 계수로 쓰는 근거는 없다. 완전히 해석한 확률표는 이 추가 조사에서 찾지 못했다.','',
            '## 패키지 안의 연출 장면 식별자','',
            '아래 목록은 압축 컨테이너의 선언 크기를 확인하고, N2 장면 청크(204)와 복합 장면 청크(207)의 고정 길이 이름 레코드를 직접 읽은 결과다. 숫자·영문 이름만으로 같은 번호의 버프/스킬과 연결하지 않았다.','']
    for resource in output['visualEffectResources']:
        lines+=['### `'+resource['entry']+'`','']
        for field,label in [('sceneChunk204','장면'),('multiSceneChunk207','복합 장면')]:
            c=resource[field]
            if c:
                lines.append(f"- {label}: {c['count']}개, 레코드 {c['stride']}바이트")
                lines.append('  '+', '.join('`'+r['name']+'`'for r in c['records']))
        lines.append('')
    lines+=['전체 이름·청크 오프셋·해시와 테이블 헤더는 `supporting-assets.json`에 보존했다. 이 자료를 효과 목록에 중복해서 새 능력으로 세지 않았다.']
    (OUT/'supporting-assets.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
    print(json.dumps({'tables':len(output['tables']),'visualResources':len(output['visualEffectResources']),
          'scenes':sum((r['sceneChunk204']or{}).get('count',0)for r in output['visualEffectResources']),
          'multiScenes':sum((r['multiSceneChunk207']or{}).get('count',0)for r in output['visualEffectResources'])}))


if __name__=='__main__':main()
