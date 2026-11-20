"""Build planning-facing ability data. Evidence stays in a separate internal artifact."""
from pathlib import Path
from collections import defaultdict,Counter
import re,json,hashlib
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Documentation/research/splus-items'
PRIVATE=OUT/'internal';PRIVATE.mkdir(exist_ok=True)
def load(n):return [json.loads(s) for s in (OUT/n).read_text(encoding='utf-8-sig').splitlines()]
CATEGORIES=[('toll','통행료','통행료를 줄이거나, 수입과 배수를 바꿉니다.'),('dice','주사위','결과·정확도·더블·추가 굴림을 바꿉니다.'),('movement','이동','도착지와 이동 경로에 개입합니다.'),('control','속박·방해','상대의 이동과 행동을 제한합니다.'),('building','건설·부동산','건설·인수·소유권을 바꿉니다.'),('defense','방어·해제','공격을 막거나 상태를 해제합니다.'),('money','자금·보상','현금과 월급, 보상을 조절합니다.'),('cards','카드·자원','카드·홀짝 등 사용할 자원을 만듭니다.'),('body','신체·거래','신체 매각과 인공 신체를 활용합니다.'),('credit','대출·이자','신용한도·상환·이자를 바꿉니다.'),('field','설치·영역','보드에 설치물과 특수 영역을 만듭니다.'),('growth','성장·복합','조건을 쌓거나 여러 효과를 연결합니다.')]
# Functional labels, deliberately independent of equipment/character names.
RULES=[
 ('통행료 재징수','toll',r'통행료.{0,16}(?:한\s*번\s*더|추가\s*지불|재징수|또\s*지불)'),
 ('통행료 면제','toll',r'통행료.{0,15}(?:면제|100%\s*할인)'),
 ('통행료 할인','toll',r'통행료.{0,30}(?:할인|감소|하락|차감)'),
 ('배수 이전·잠금','toll',r'배수.{0,12}(?:이전|잠금|고정)|배수\s*이전'),
 ('통행료 강화','toll',r'통행료.{0,35}(?:증가|증폭|상승|배|추가\s*징수)'),
 ('건설비 할인','building',r'건설\s*비(?:용)?.{0,25}(?:할인|감소|면제)|무료.{0,6}건설'),
 ('인수비 할인','building',r'인수\s*(?:비용|금액|비).{0,25}(?:할인|감소|면제)'),
 ('랜드마크 자동 건설','building',r'랜드마크(?:로)?\s*자동\s*건설|랜드마크로.{0,12}(?:건설|업그레이드)|랜드마크\s*건설(?!\s*(?:시|및|성공|후|후에))'),
 ('건물 파괴','building',r'건물.{0,15}(?:파괴|철거|붕괴)|(?:파괴|철거).{0,15}건물'),
 ('부동산 강탈','building',r'(?:지역|땅|랜드마크).{0,20}(?:강탈|빼앗|기부|뺏)|(?:강탈|기부).{0,20}(?:지역|땅|랜드마크)'),
 ('부동산 인수','building',r'강제\s*인수|(?:도착|건설|지역).{0,30}인수'),
 ('소유권 전환','building',r'페인트|소유권.{0,15}(?:변경|이전)|내\s*소유물'),
 ('공격 반사','defense',r'반사|반격'),
 ('천사카드 무력화','control',r'천사\s*카드.{0,18}무력화(?!\s*방어)'),
 ('강제 이동 방어','defense',r'강제\s*이동.{0,35}방어'),
 ('속박 해제','defense',r'(?:속박|감옥|비눗방울).{0,25}(?:해제|탈출)'),
 ('공격 방어','defense',r'방어|보호막|쉴드|실드|무적'),
 ('비눗방울 속박','control',r'비눗방울|물방울'),
 ('이동 봉쇄','control',r'이동\s*(?:봉인|불가|금지|제한)|감옥\s*속박'),
 ('상대 붙잡기','control',r'붙잡|경유\s*정지'),
 ('상대 끌어당김','control',r'끌어\s*당|끌어당|끌어옴|당겨'),
 ('상대 밀어내기','control',r'밀어\s*내|날려\s*보|밀쳐'),
 ('주사위 취소','control',r'주사위.{0,18}취소'),
 ('행동 강제 종료','control',r'턴.{0,12}(?:강제\s*)?종료'),
 ('스킬 무력화','control',r'(?:스킬|능력).{0,20}(?:무력화|봉인|취소)|호출.{0,10}취소|허수아비'),
 ('방어 관통','control',r'방어.{0,15}무시|무시.{0,15}방어'),
 ('주사위 컨트롤 강화','dice',r'주사위\s*컨트롤|주컨|주사위\s*게이지'),
 ('더블 확률 증가','dice',r'더블.{0,15}(?:확률|증가)|첫.{0,12}더블'),
 ('추가 주사위','dice',r'주사위.{0,22}(?:한\s*번\s*더|1회\s*(?:찬스|기회|굴)|추가)|추가.{0,8}굴림'),
 ('주사위 배수 이동','dice',r'주사위.{0,25}(?:배|눈금)|결과.{0,18}배'),
 ('홀짝 생성','cards',r'홀짝.{0,20}(?:생성|추가|획득|소지)'),
 ('선택 순간이동','movement',r'(?:선택|원하는).{0,25}(?:순간\s*이동|이동)|순간\s*이동'),
 ('세계여행 즉시 이용','movement',r'세계여행.{0,40}(?:즉시|대기|기다리)'),
 ('세계여행 이동','movement',r'세계여행.{0,12}이동|이동.{0,12}세계여행'),
 ('출발지 이동','movement',r'(?:시작\s*지점|출발지).{0,18}이동|이동.{0,12}(?:시작\s*지점|출발지)'),
 ('무인도 이동','movement',r'무인도.{0,20}(?:강제\s*)?이동|무인도\s*유배'),
 ('무인도 탈출','movement',r'무인도.{0,25}탈출'),
 ('역방향 이동','movement',r'뒤로\s*이동|역주행|후진'),
 ('위치 교환','movement',r'위치.{0,20}(?:교환|바꿔)|자리.{0,12}교환'),
 ('이동 거리 변경','movement',r'(?:칸|이동\s*거리).{0,20}(?:증가|감소|추가)|\d+칸.{0,8}(?:전진|후진)'),
 ('마블 강탈','money',r'(?:마블|금액|머니).{0,25}(?:강탈|빼앗|뺏)|강탈.{0,15}마블'),
 ('월급 추가 획득','money',r'월급.{0,35}(?:획득|지급|추가|증가)'),
 ('시작 자금 증가','money',r'(?:시작|첫\s*턴).{0,25}(?:보유\s*마블|자금).{0,20}증가'),
 ('현금 보상','money',r'(?:마블|금액|머니).{0,25}(?:획득|지급|보상|증가|돌려)|건설\s*비용.{0,20}획득'),
 ('대출 추가 보상','credit',r'대출.{0,30}(?:추가|획득|할인)'),
 ('카드 획득','cards',r'(?:카드|쿠폰).{0,18}(?:획득|생성)|획득.{0,18}(?:카드|쿠폰)'),
 ('카드 강탈','cards',r'(?:카드|쿠폰).{0,15}강탈'),
 ('블랙홀 설치','field',r'블랙홀.{0,25}(?:설치|생성)|(?:설치|생성).{0,25}블랙홀'),
 ('전자기장 설치','field',r'전자기장'),('함정 설치','field',r'(?:트랩|함정|폭탄|검문소|자석|덩굴).{0,25}(?:설치|생성|발동)'),
 ('확률 성장','growth',r'성장|충전|초기화'),('효과 복사','growth',r'복사|복제|카피|스킬\s*획득'),
]
REJECT=re.compile(r'중\s*\d+종|중\s*1종|랜덤\s*등장|수호석|결정\s*보석|유리\s*보석|영롱|보물|골드\s*보너스|고정합성|합성해서|도전하세요|도전!|교환\s*이벤트|추천\s*보석|최초\s*획득|신비한\s*힘|스페셜빛|한정|가격\s*할인|골드로|캐릭터카드|캐릭터\s*카드|보조옵|버프\s*지급|출시|쿠폰\s*번호|새싹\s*유저|축하\s*헬멧|전설적인\s*레이서|이벤트\s*기간|패키지|우편함|접속|출석|뽑기권|카드팩|교환\s*보상|수집\s*보상|도감\s*보상|누적\s*구매|판매\s*기간|강화\s*재료|재합성하면|합성\s*시|합성으로|확정\s*획득|기념\s*상품|사용\s*할까요|하시겠|부여할\s*수|확인\s*할\s*수|알아볼까요|개발중|테스트용')
OP=re.compile(r'확률|%|##skill|\d+\s*(?:칸|턴|배|회)|(?:도착|통과|건설|더블|시작|해제).{0,25}(?:획득|이동|강탈|방어|파괴|할인|증가|발동|생성|속박)')
def clean(raw):
 t=re.sub(r'</?(?:tooltip|style|font|color|size)(?:=[^>]*|\s[^>]*)?>','',raw,flags=re.I)
 t=re.sub(r'<br\s*/?>',' ',t,flags=re.I)
 t=re.sub(r'<+\[?발동\s*예외사항\d*\]?>+|\[발동\s*예외사항\d*\]','',t)
 t=re.sub(r'<[^>]*>','',t)
 t=re.sub(r'\[(?:MAX|최대)[^\]]*\]','',t,flags=re.I)
 t=re.sub(r'(?:MAX\s*)?Lv\.?\s*\d+\s*(?:강화\s*시|강화시)?','',t,flags=re.I)
 t=re.sub(r'MAX\s*(?:강화\s*시|강화시|\d*강|효과)?','',t,flags=re.I)
 t=re.sub(r'\[[^\]]*(?:강|기준|효과)[^\]]*\]','',t)
 t=re.sub(r'^.{0,100}잠재된 능력이 해방됩니다[.]?\s*','',t)
 t=re.sub(r'^S\+.{0,40}장착\s*시\s*','',t)
 t=re.sub(r'\d{4}년.{0,60}(?:적용됩니다|종료|까지)[.]?','',t)
 t=re.sub(r'\([^)]*(?:획득 후|획득후|판 제한|시간동안 사용|시간 동안 사용)[^)]*\)','',t)
 t=re.sub(r'\([^)]*(?:헬멧|헬맷|설계도면|예고장|향수|채찍|가이드북)[^)]*\)',lambda m:'(일부 효과 제외)' if '제외' in m[0] else '(특정 방어 효과 무시)' if '무시' in m[0] else '',t)
 t=re.sub(r'##[^#]+##\s*%','N%',t)
 t=re.sub(r'##[^#]+##','N',t)
 t=re.sub(r'%[sdifu]','N',t)
 t=re.sub(r'\[[^\]]*\]','',t)
 t=re.sub(r'^(?:\s*[/\]]+\s*|\s*(?:기준\s*효과|기준|효과)\s*[:：]\s*)','',t)
 t=re.sub(r'(?:조건|효과|보조|연쇄)\s*[:：]\s*','',t)
 t=re.sub(r'\s+',' ',t).strip(' /,\n')
 t=t.replace('시작지점','출발지').replace('시작 지점','출발지').replace('주컨','주사위 컨트롤')
 t=re.sub(r'\s+([,.)])',r'\1',t)
 t=re.sub(r'^강화\s*시\s*','',t)
 return t

def functional(t):
 matches=[(name,cat) for name,cat,pat in RULES if re.search(pat,t)]
 if not matches:return None
 # Prefer actual outcomes over a defensive context used as an exception.
 first=matches[0]
 if re.search(r'방어.{0,12}무시',t):first=('방어 관통','control')
 if re.search(r'블랙홀.{0,25}(?:설치|생성)|(?:설치|생성).{0,25}블랙홀',t):first=('블랙홀 설치','field')
 if re.search(r'주사위.{0,22}(?:한\s*번\s*더|1회\s*(?:찬스|기회|굴)|추가)',t):first=('추가 주사위','dice')
 if re.search(r'주사위\s*컨트롤|주컨',t) and not re.search(r'(?:속박|면제|무력화|블랙홀|끌어당|통행료)',t):first=('주사위 컨트롤 강화','dice')
 if re.search(r'(?:무력화|봉인).{0,8}방어',t):first=('무력화 방어','defense')
 if re.search(r'통행료\s*면제.{0,12}(?:무시|무력화)',t):first=('통행료 면제 무효','control')
 if re.search(r'주사위\s*컨트롤.{0,25}(?:감소|하락)',t):first=('주사위 컨트롤 약화','control')
 if re.search(r'더블.{0,18}(?:감소|하락)',t):first=('더블 확률 감소','control')
 if re.search(r'상대\s*일단\s*정지',t):first=('상대 붙잡기','control')
 if re.search(r'(?:블랙홀|화이트홀).{0,50}바꿔치기',t):first=('포털 위치 교환','field')
 if re.search(r'스프링\s*트랩.{0,40}(?:소유|바꿔)',t):first=('함정 소유권 탈취','field')
 if re.search(r'세계\s*여행.{0,35}취소',t):first=('세계여행 방해','control')
 if re.search(r'방어\s*카드.{0,15}무력화',t):first=('방어카드 무력화','control')
 if re.search(r'대출.{0,20}할인',t):first=('대출 비용 할인','credit')
 return first

def trigger(t):
 rules=[('내 턴 시작',r'내\s*턴\s*시작'),('상대 도착',r'상대(?:방)?이?\s*(?:내|자신|나에게).{0,15}도착'),('내 랜드마크 도착',r'내\s*랜드마크\s*도착'),('상대 땅 도착',r'상대\s*(?:땅|지역)\s*도착'),('내 땅 도착',r'내\s*(?:땅|지역)\s*도착'),('랜드마크 건설',r'랜드마크\s*건설'),('건설',r'건물\s*3개\s*건설|건설\s*시'),('출발지 통과',r'(?:출발지|시작지점).{0,12}(?:경유|지나|통과)'),('더블',r'더블\s*(?:시|발동)'),('독점 경고',r'독점\s*(?:경고|찬스)'),('통과',r'지나칠|통과\s*시'),('인수',r'인수\s*시'),('게임 시작',r'게임\s*시작'),('공격',r'공격\s*시'),('속박 해제',r'속박\s*해제')]
 return next((name for name,pat in rules if re.search(pat,t)),'조건 발동')

def signature(t):
 t=re.sub(r'설정 확률|설정값|N|\d+(?:\.\d+)?','{N}',t)
 return re.sub(r'\s+','',t).replace('확률로','로').replace('확률','')
entries={};provenance=defaultdict(list);rejects=[]
def add(title,effect,category,origin,refs,kind='research',module=None):
 key=(signature(effect),title,category)
 uid='ability-'+hashlib.sha256('|'.join(key).encode()).hexdigest()[:12]
 if uid not in entries:entries[uid]={'id':uid,'name':title,'category':category,'trigger':trigger(effect),'origins':[],'variants':[],'tags':[]}
 e=entries[uid]
 if origin not in e['origins']:e['origins'].append(origin)
 if effect not in [v['effect'] for v in e['variants']]:e['variants'].append({'effect':effect,'adjustable':'N' in effect})
 if kind=='module':e['tags'].append('제작 모듈')
 provenance[uid].append({'origin':origin,'refs':refs,'module':module})
 return uid

def harvest(raw,origin,refs):
 if not raw or len(raw)>850 or REJECT.search(raw):return
 t=clean(raw)
 if len(t)<12 or len(t)>600 or not OP.search(raw):return
 if t.count('(')!=t.count(')') or re.search(r'(?:하고|하며|한다면|일때|등|경우|따라|또는)\s*$',t):return
 if re.search(r'\b헬멧류\s*능력치',t):t=re.sub(r'헬멧류\s*능력치','주사위 컨트롤 정확도',t)
 if re.search(r'설정|보조\s*:|S\+|A클래스|캐릭터|장착|해방|##|<|MAX|Lv\.|카드\s*\d+장|상품|게임\s*설명|합니다\.|모아|강화 가능',t):return
 if re.search(r'(?:화시|화 시|^화로|^기준|^강화)',t):return
 if re.match(r'^(?:상대|본인|자신|팀원|특정|해당|내).{0,20}(?:불가|금지|없음)$',t):return
 if re.search(r'소모\s*시|장착\s*가능|강화\s*확률|획득\s*가능|사용\s*불가|[!?]$',t) and not re.search(r'확률|%|##skill',raw):return
 f=functional(t)
 if not f:return
 # Unspecified skill-only display fragments do not constitute a usable ability.
 if re.match(r'^(?:설정 확률|\d+%).{0,6}(?:로\s*)?발동',t) or re.fullmatch(r'.{0,12}(?:효과\s*증가|확률\s*증가|능력치\s*증가)',t):return
 if re.search(r'게임\s*설명|카드\s*등급|MAX\s*레벨|레벨업|등록|지원하지',t):return
 add(f[0],t,f[1],origin,refs)

main=load('main_catalog.jsonl')
for r in main:
 if r['reviewStatus']!='contextual-main-candidate':continue
 for e in r['effectEvidence']:harvest(e['text'],'행운아이템 조사',[e['recordId']])
for r in load('aux_candidates.jsonl'):
 if r['evidenceClass']=='aux-effect-bearing-promotion-needs-review' or r.get('textShape') in ['exception-fragment','name-fragment']:continue
 harvest(r['effectText'],'행운아이템 조사',[r['source']['recordId']])
# The old catalog's unlinked names are deliberately not a user-facing concept.
# Keep only standalone actionable effect clauses; no item identity is exposed.
for r in load('main_unlinked_effect_candidates.jsonl'):
 if r.get('promotionOrAuxiliaryContext'):continue
 harvest(r['evidence']['text'],'행운아이템 조사',[r['evidence']['recordId']])

# Explicit project modules: UI descriptions are reviewed, engine facts remain private.
MODULES=[
 ('toll.discount','통행료 할인','지불하는 통행료를 25% 줄인다.','toll'),('toll.free','통행료 면제','통행료를 1회 면제한다.','toll'),('toll.flat','통행료 정액 할인','지불할 통행료에서 100G를 차감한다.','toll'),('toll.bonus','통행료 수입 증가','통행료 수입을 30% 늘린다.','toll'),('toll.tip','통행료 추가 수입','통행료 수입에 50G를 더한다.','toll'),
 ('build.discount','건설비 할인','건설비를 20% 줄인다.','building'),('build.free','무료 건설','건설비를 1회 면제한다.','building'),('purchase.discount','땅값 할인','땅 구매비를 15% 줄인다.','building'),('purchase.coupon','땅 구매 쿠폰','땅 구매비에서 100G를 1회 차감한다.','building'),('salary.bonus','월급 인상','월급을 25% 늘린다.','money'),('salary.tip','월급 추가 지급','월급에 100G를 더한다.','money'),('tax.discount','세금 감면','세금을 50% 줄인다.','money'),('tax.free','세금 면제','세금을 1회 면제한다.','money'),('dice.plus','주사위 합 증가','주사위 합계를 1 늘린다.','dice'),('dice.minus','주사위 합 감소','주사위 합계를 1 줄인다.','dice'),('move.plus','추가 이동','이동 거리를 2칸 늘린다.','movement'),('move.half','이동 거리 반감','이동 거리를 절반으로 줄인다.','movement'),('move.forward','착지 후 전진','땅에 도착한 뒤 2칸 더 전진한다. 같은 턴 중복 발동을 막는다.','movement'),('move.backward','착지 후 후진','땅에 도착한 뒤 1칸 후진한다. 같은 턴 중복 발동을 막는다.','movement'),('move.start','출발지 귀환','턴 시작에 출발지로 이동한다. 게임당 1회 사용한다.','movement'),
 ('status.bubble','물방울 가두기','공격 시 35% 확률로 상대에게 1턴 물방울 속박을 부여한다. 재사용 간격은 2턴이다.','control'),('status.root','이동 봉쇄','공격한 상대에게 1턴 이동 봉쇄를 부여한다.','control'),('status.reverse','역방향 저주','공격한 상대에게 2턴 역방향 이동 상태를 부여한다.','control'),('status.toll-shield','통행료 방어막','턴 시작에 1턴 통행료 방어막을 부여한다.','defense'),('status.attack-shield','공격 방어막','턴 시작에 1턴 공격 방어막을 부여한다.','defense'),('status.reflect','공격 반사','턴 시작에 1턴 공격 반사 상태를 부여한다.','defense'),('status.pull','상대 끌어당김','공격 대상에게 끌어당기기 효과를 부여한다. 거리와 도착지는 기획에 맞춰 정한다.','control'),('status.push','상대 밀어내기','공격 대상에게 밀어내기 효과를 부여한다. 거리와 도착지는 기획에 맞춰 정한다.','control'),('status.steal-block','강제 인수 방어','턴 시작에 2턴 강제 인수 방어 상태를 부여한다.','defense'),('status.build-block','건설 봉쇄','공격 대상에게 1턴 건설 봉쇄를 부여한다.','control'),('status.toll-boost','타일 통행료 강화','도착한 타일에 2턴 통행료 강화 효과를 부여한다. 배율은 기획에 맞춰 정한다.','toll'),('status.toll-suppression','타일 통행료 억제','도착한 타일에 2턴 통행료 억제 효과를 부여한다. 감소량은 기획에 맞춰 정한다.','toll')]
SPECIAL=[
 ('DiceCheat','주사위 ±1 조정','본인 3턴마다 주사위 합계를 +1 또는 -1 조정한다. 합계는 2~12를 유지한다.','dice'),('Pickpocket','통과 마블 강탈','상대를 지나칠 때 보유 현금에서 최대 30G를 가져온다.','money'),('SlipperyFloor','방문자 추가 이동','내 땅에 도착한 상대를 2칸 더 이동시킨다.','movement'),('IllegalParking','다음 이동 감소','내 땅에 도착한 상대의 다음 이동 거리를 2칸 줄인다. 1회 적용 후 해제한다.','control'),('PropertyBroker','빈 땅 구매 할인','빈 땅 구매비를 15% 줄인다.','building'),('HiddenCamera','첫 방문 추가 통행료','각 상대가 내 땅에서 처음 통행료를 낼 때 50G를 추가 청구한다.','toll'),('Roadblock','주기적 이동 방해','본인 3턴마다 지정 상대의 다음 이동 거리를 1칸 줄인다.','control'),('UninvitedGuest','상대 땅 통행료 할인','상대 소유 땅에 지불하는 통행료를 15% 줄인다.','toll'),('LuckyPrank','더블 현금 보상','더블이 나오면 50G를 얻는다.','money'),('InsuranceFraud','건물 피해 보상','상대 공격으로 내 건물이 손실되거나 기능 정지되면 100G를 받는다.','money'),('InterestCoupon','대출 이자 감면','대출 이자율을 2%p 줄인다. 최저 이자율은 0%다.','credit'),('DamageRefund','통행료 지연 환급','실제로 낸 통행료의 10%를 본인 다음 턴 시작에 돌려받는다.','toll'),('BlackMarket','신체 매각가 증가','신체 매각 대금을 10% 늘린다.','body'),('BodyInvestment','신체 매각 후 건설 할인','신체를 팔면 다음 성공한 건설의 비용을 20% 줄인다. 할인은 중첩 적립하지 않는다.','body'),
 ('BuildingThief','건물 이전 강탈','상대의 가장 높은 일반 건물 1개를 내 땅으로 이전한다. 중복 종류와 건설 해금 제한을 지킨다.','building'),('PropertyBomber','통행료 수익 정지','선택한 상대 땅의 통행료를 소유자 턴 기준 2턴 경계까지 정지한다.','toll'),('EvilLandlord','통행료 수입 강화','내 통행료를 40% 늘린다.','toll'),('PositionSwap','상대와 위치 교환','원하는 상대와 현재 위치를 맞바꾼다.','movement'),('DiceHacker','주사위 재굴림','본인 3턴마다 주사위 2개를 다시 굴릴 수 있다.','dice'),('ChainBomb','통행료 이월 청구','내 땅에서 받은 통행료의 20%를 그 땅의 다음 통행료에 더한다.','toll'),('TaxCollector','부자에게 세금 징수','상대 중 현금이 가장 많은 사람에게 최대 100G를 징수한다.','money'),('ForcedAuction','부동산 강제 경매','부채 500G 이상이고 일반 건물이 1개 이상인 상대 땅을 경매한다. 입찰금은 예치하고 낙찰 시 정산한다.','building'),('Redevelopment','건물 희생 재개발','내 일반 건물 1개를 희생해 다른 내 땅에 해금된 미보유 건물을 추가한다.','building'),('Revenge','최다 통행료 상대 복수','나에게 가장 많은 통행료를 받은 상대를 공격할 때 공격 수치를 25% 늘린다.','growth'),('DebtDevil','부채 비례 통행료','부채 100G마다 통행료를 5% 늘린다. 추가 보너스 상한은 100%다.','credit'),('BodyMoney','신체 매각 대금 강화','신체 매각 대금을 20% 늘린다.','body'),('CyborgLandlord','인공 신체 통행료','인공 신체 1개당 통행료를 5% 늘린다. 기본 대상은 양쪽 다리·폐·심장이다.','body'),('BadCreditTycoon','신용한도 확대','신용한도를 50% 늘린다. 상환 기한은 유지한다.','credit'),('DeadlineExtension','상환 기한 연장','부채가 있을 때 상환 만기를 1라운드 연장한다. 게임당 1회 사용한다.','credit'),('HealthyCapital','신체 미매각 수익','게임 중 신체를 한 번도 팔지 않았다면 통행료를 15% 늘린다.','body')]
for module,title,effect,category in MODULES:add(title,effect,category,'제작 능력',[],kind='module',module='AbilityModules:'+module)
for module,title,effect,category in SPECIAL:add(title,effect,category,'제작 능력',[],kind='module',module='SpecialMechanics:'+module)

def skill_values(text):
 # Keep structural dice bounds; expose balance values without choosing defaults.
 text=text.replace('2~12','__DICE_BOUNDS__')
 text=text.replace('최저 이자율은 0%','최저 이자율은 __ZERO_FLOOR__%')
 text=text.replace('이동 거리를 절반으로 줄인다.','이동 거리를 N% 줄인다.')
 text=re.sub(r'한\s*번\s*더','N회 추가',text)
 counter=0
 def slot(match):
  nonlocal counter
  counter+=1
  return '(스킬밸류'+(str(counter) if counter>1 else '')+')'
 text=re.sub(r'\d+(?:\.\d+)?|(?<![A-Za-z_])N(?![A-Za-z_])',slot,text)
 return text.replace('__DICE_BOUNDS__','2~12').replace('__ZERO_FLOOR__','0')

public=[]
for e in entries.values():
 # Concrete values are more useful than parameterized versions as the initial card text.
 # Only display parameterized templates. Original variants stay in the internal map.
 originals=list(e['variants'])
 for record in provenance[e['id']]:record['originalDisplayVariants']=originals
 unique={}
 for v in originals:
  text=skill_values(v['effect'])
  unique.setdefault(text,{'effect':text,'adjustable':'(스킬밸류' in text})
 e['variants']=list(unique.values())
 if e['name']=='주사위 ±1 조정':e['name']='주사위 합 조정'
 if e['name']=='이동 거리 반감':e['name']='이동 거리 감소'
 e['variants'].sort(key=lambda v:len(v['effect']))
 e['effect']=e['variants'][0]['effect'];e['tags']=sorted(set(e['tags']))
 public.append(e)
order={c[0]:i for i,c in enumerate(CATEGORIES)}
public.sort(key=lambda e:(order[e['category']],0 if '제작 능력' in e['origins'] else 1,e['name'],e['id']))
meta={'title':'능력 설계실','categories':[{'id':a,'name':b,'description':c} for a,b,c in CATEGORIES],
 'total':len(public),'moduleCount':len(MODULES)+len(SPECIAL),'researchCount':sum('행운아이템 조사' in e['origins'] for e in public),
 'variantCount':sum(len(e['variants']) for e in public),'version':2}
(OUT/'ability-data.js').write_text('"use strict";\nwindow.ABILITY_CATALOG='+json.dumps({'meta':meta,'abilities':public},ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
(PRIVATE/'ability-provenance.json').write_text(json.dumps(dict(provenance),ensure_ascii=False,indent=2),encoding='utf-8')
(PRIVATE/'ability-build-summary.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(meta,ensure_ascii=False))
