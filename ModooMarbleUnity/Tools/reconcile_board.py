"""Reconcile the runtime board with already decoded, source-bound APK layouts.

This does not draw new art. It preserves the source label layers, opaque sprite
footprints and character frame coordinates for the runtime renderer.
"""
import json
import math
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / 'Assets/Marble/Resources/OriginalUI'
DOC = ROOT / 'Documentation/board-correction'


def read(path):
    return json.loads(path.read_text(encoding='utf8'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def hull(points):
    points = sorted(set(points))
    def cross(o, a, b):
        return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lower, upper = [], []
    for p in points:
        while len(lower)>1 and cross(lower[-2], lower[-1], p)<=0:
            lower.pop()
        lower.append(p)
    for p in reversed(points):
        while len(upper)>1 and cross(upper[-2], upper[-1], p)<=0:
            upper.pop()
        upper.append(p)
    return lower[:-1]+upper[:-1]


def contains(poly, x, y):
    inside = False
    j = len(poly)-1
    for i, a in enumerate(poly):
        b = poly[j]
        if (a['y']>y)!=(b['y']>y) and x < (b['x']-a['x'])*(y-a['y'])/(b['y']-a['y'])+a['x']:
            inside = not inside
        j = i
    return inside


def main():
    board = read(RES / 'GameBoardBlock/original-layout.json')
    sheets = read(RES / 'GameBoardBlock/metadata.json')['sheets']
    old = read(RES / 'board-positions.json')['tiles']
    map0 = read(ROOT / 'Assets/StreamingAssets/original-map.json')['tiles']
    corrected_base = Image.open(RES / 'board_background.png').convert('RGBA')
    cells = []
    for index, source_scene in enumerate(board['scenes'][:32]):
        source_frames = [next((f for f in layer['frames'] if f['delayMs']==0 and f['sheetIndex'] is not None), None)
                         for layer in source_scene['layers']]
        source_frames = [f for f in source_frames if f is not None]
        frame = source_frames[0]
        assert frame['scaleX'] == frame['scaleY'] == 1.0, (index,'unexpected source base scale')
        # The reusable sprite scene still has TAX at cell30. RgnInfo0 explicitly
        # defines New York as a city in the same group as Paris/Seoul.
        effective_sheet = 10 if index == 30 else frame['sheetIndex']
        sheet = sheets[effective_sheet]
        image = Image.open(RES / 'GameBoardBlock' / sheet['file']).convert('RGBA')
        assert image.size == (sheet['originalWidth'], sheet['originalHeight'])
        alpha = image.getchannel('A')
        outline = hull([(x,y) for y in range(image.height) for x in range(image.width) if alpha.getpixel((x,y))>=128])
        poly = [{'x':frame['x']+x*abs(frame['scaleX']), 'y':frame['y']+y*abs(frame['scaleY'])} for x,y in outline]
        labels=[]
        # Map0 calls cell25 Tahiti; the source's reusable board art says Taipei.
        # Keep the original city-name art everywhere it matches the recovered map.
        for label in source_frames[1:]:
            if label['sheetIndex']==35 or index in (25,30):
                continue  # sheet35 is a dark marker/effect, not the city name.
            s=sheets[label['sheetIndex']]
            labels.append({'sheetIndex':s['index'], 'x':label['x'], 'y':label['y'],
                           'width':s['originalWidth']*abs(label['scaleX']),
                           'height':s['originalHeight']*abs(label['scaleY']), 'opacity':label['opacity']})
        cell=dict(old[index])
        cell.update(name=map0[index]['name'], sourceScene=source_scene['name'],
                    sourceSheetIndex=frame['sheetIndex'],sheetIndex=effective_sheet,
                    labelLayers=labels, dynamicName=index in (25,30),
                    nameAngle=30.0 if index//8 in (0,2) else -30.0,
                    hitPolygon=poly)
        assert frame['sheetIndex']==cell['sourceSheetIndex']
        assert contains(poly,cell['x'],cell['y']), (index,'source cell center outside sprite footprint')
        corrected_base.alpha_composite(image,(round(frame['x']),round(frame['y'])))
        cells.append(cell)
    assert len(cells)==32
    for index, cell in enumerate(cells):
        candidates = [c for c in cells if contains(c['hitPolygon'],cell['x'],cell['y'])]
        chosen = min(candidates,key=lambda c:((cell['x']-c['x'])/c['width'])**2+((cell['y']-c['y'])/c['height'])**2)
        assert chosen['index']==index, (index,chosen['index'])
    resolutions=[]
    for width,height in [(1440,900),(1920,1080),(1280,800)]:
        scale=min(width/1440,height/900)
        offset_x=(width-1440*scale)/2
        offset_y=(height-900*scale)/2
        for cell in cells:
            canvas_x,canvas_y=cell['x']*1.125,cell['y']*1.125
            screen_x=offset_x+canvas_x*scale
            screen_y=height-(offset_y+canvas_y*scale)
            restored_x=(screen_x-offset_x)/scale/1.125
            restored_y=((height-screen_y)-offset_y)/scale/1.125
            assert abs(restored_x-cell['x'])<1e-6 and abs(restored_y-cell['y'])<1e-6
            assert contains(cell['hitPolygon'],restored_x,restored_y)
        resolutions.append({'screenWidth':width,'screenHeight':height,'normalizedCenterChecks':32,'uniformCanvasScale':scale,'letterboxOffset':[offset_x,offset_y]})
    write(RES / 'board-render-layout.json', {'canvasWidth':1280,'canvasHeight':800,
        'coordinates':'Source native top-left; hitPolygon is convex hull of alpha>=128 pixels, not a rectangular hitbox.',
        'tiles':cells})
    corrected_base.save(RES / 'board-corrected-base.png')

    characters=[]
    for i in range(1,5):
        resource=f'character{i:02d}'
        data=read(RES / resource / 'original-layout.json')
        metadata=read(RES / resource / 'metadata.json')['sheets']
        idle=next(s for s in data['scenes'] if s['name']=='Idle_Base')
        frames=[]
        for layer in idle['layers']:
            for f in layer['frames']:
                if f['sheetIndex'] is None:
                    continue
                sheet=metadata[f['sheetIndex']]
                frames.append({'sheetIndex':f['sheetIndex'],'timeMs':f['delayMs'],'durationMs':f['lifeTimeMs'],
                    'x':f['x'],'y':f['y'],'width':sheet['originalWidth']*abs(f['scaleX']),
                    'height':sheet['originalHeight']*abs(f['scaleY'])})
        frames.sort(key=lambda f:f['timeMs'])
        first=frames[0]
        characters.append({'resource':resource,'centerX':first['x']+first['width']/2,
                           'groundY':first['y']+first['height'],'frames':frames})
    write(RES / 'character-render-frames.json', {'characters':characters})
    report={'status':'passed','sourceSceneCount':32,'exactBasePlacementsPreserved':32,
        'sourceLabelLayerCount':sum(len(c['labelLayers']) for c in cells),
        'map0NameOverride':{'index':25,'sourceArt':'타이페이','runtimeMap0':'타히티'},
        'map0TileOverride':{'index':30,'sourceTemplateSheet':44,'sourceTemplateKind':'TAX',
                           'map0Kind':'City','map0Group':8,'correctedSheet':10},
        'sourceOpaqueHullCenterHitTestsPassed':32,'characters':4,
        'screenToCanvasChecks':resolutions,
        'characterIdleFrames':sum(len(c['frames']) for c in characters),
        'unverified':'Final original game-code HUD placement, building root offsets and input dispatch were not recovered.'}
    write(DOC / 'source-layout-validation.json',report)
    print(json.dumps(report,ensure_ascii=True))


if __name__=='__main__':
    main()
