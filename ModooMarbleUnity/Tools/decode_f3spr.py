"""Decode bounded user-supplied F3SPR UI resource atlases to PNG.

Supports F3ZT_SPR/N2 binary records and XML F3Sprite; RGBA8888,
RGBA4444, RGBA5551, and ETC1 RGB + separate 8-bit alpha (format10000).
Outputs source crops and metadata; does not execute packaged code.
"""
from pathlib import Path
import base64
import hashlib
import json
import math
import struct
import sys
import xml.etree.ElementTree as ET
import zipfile
import zlib
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/Marble/Resources/OriginalUI"
DOC = ROOT / "Documentation/apk/ui"
APK = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(r"C:\Users\ksh00\Downloads\모두의마블_10.9.02.apk")
NAMES = ['hud', 'GameBoardBlock', 'GameBtnDice', 'SinglePlay', 'GameDiceGage', 'character01', 'character02', 'character03', 'character04', 'DiceInfo', 'mapinfo', 'pop_buy_infor', 'oGameStructInfo', 'common_pop']
MODIFIERS = [(2,8,-2,-8),(5,17,-5,-17),(9,29,-9,-29),(13,42,-13,-42),(18,60,-18,-60),(24,80,-24,-80),(33,106,-33,-106),(47,183,-47,-183)]


def etc1(data, w, h):
    out = np.zeros((h, w, 4), dtype=np.uint8)
    def sign3(v): return v - 8 if v & 4 else v
    def five(v): return (v << 3) | (v >> 2)
    for by in range(0, h, 4):
        for bx in range(0, w, 4):
            offset = ((by // 4) * ((w + 3) // 4) + bx // 4) * 8
            block = int.from_bytes(data[offset:offset+8], 'big')
            high = block >> 32
            diff, flip = (high >> 1) & 1, high & 1
            if diff:
                rgb1 = [(high >> 27) & 31, (high >> 19) & 31, (high >> 11) & 31]
                delta = [sign3((high >> 24) & 7), sign3((high >> 16) & 7), sign3((high >> 8) & 7)]
                rgb2 = [a+b for a,b in zip(rgb1,delta)]
                colors = [list(map(five,rgb1)), list(map(five,rgb2))]
            else:
                colors = [[((high >> shift) & 15)*17 for shift in shifts] for shifts in [(28,20,12),(24,16,8)]]
            tables = [(high >> 5) & 7, (high >> 2) & 7]
            low = block & 0xffffffff
            for y in range(4):
                for x in range(4):
                    if by+y >= h or bx+x >= w: continue
                    k = x*4+y
                    index = ((low >> k) & 1) | (((low >> (k+16)) & 1) << 1)
                    region = int(y >= 2) if flip else int(x >= 2)
                    mod = MODIFIERS[tables[region]][index]
                    out[by+y,bx+x,:3] = [max(0,min(255,c+mod)) for c in colors[region]]
                    out[by+y,bx+x,3] = 255
    return out


def pixels(raw, w, h, fmt, alpha=None):
    if fmt == 8888:
        assert len(raw) == w*h*4
        arr = np.frombuffer(raw,dtype=np.uint8).reshape(h,w,4).copy()
    elif fmt in (4444,5551):
        assert len(raw) == w*h*2
        v = np.frombuffer(raw,dtype='<u2').reshape(h,w)
        arr = np.zeros((h,w,4),dtype=np.uint8)
        if fmt == 4444:
            for k,shift in enumerate((12,8,4,0)):arr[:,:,k] = ((v >> shift) & 15)*17
        else:
            for k,shift in enumerate((11,6,1)):
                a = ((v >> shift) & 31);arr[:,:,k] = (a << 3) | (a >> 2)
            arr[:,:,3] = (v & 1)*255
    elif fmt == 10000:
        assert len(raw) == ((w+3)//4)*((h+3)//4)*8
        arr = etc1(raw,w,h)
        if alpha is not None:
            assert len(alpha) == w*h
            arr[:,:,3] = np.frombuffer(alpha,dtype=np.uint8).reshape(h,w)
    else:
        raise ValueError('Unsupported pixel format '+str(fmt))
    return Image.fromarray(arr, 'RGBA')


def write_json(path, data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')


def binary(data):
    declared_length=struct.unpack_from('<Q',data,20)[0]
    blob = data[40:] if data[40:42]==b'N2' else zlib.decompress(data[40:])
    if declared_length:assert len(blob) == declared_length
    assert blob[:2] == b'N2'
    table_start = struct.unpack_from('<I',blob,4)[0] + 12
    def chunk(tag):
        start = blob.find(b'N2'+bytes([tag,0]), table_start)
        if start < 0: return []
        count,stride=struct.unpack_from('<II',blob,start+4)
        assert stride < 2048 and count < 100000
        return [blob[start+12+i*stride:start+12+(i+1)*stride] for i in range(count)]
    def text(off,size): return blob[12+off:12+off+size].rstrip(b'\0').decode('cp949','replace')
    atlases=[]
    for rec in chunk(201):
        a=struct.unpack('<18I',rec)
        compressed=blob[12+a[11]:12+a[11]+a[12]]
        raw=zlib.decompress(compressed)
        assert len(raw)==a[16]
        alpha=zlib.decompress(blob[12+a[11]+a[12]:12+a[11]+a[12]+a[13]]) if a[13] else None
        if alpha is not None:assert len(alpha)==a[17]
        atlases.append({'id':a[2],'uuid':text(a[0],a[1]),'width':a[3],'height':a[4],'pixelFormat':a[5],'sheetStart':a[14],'sheetCount':a[15],'image':pixels(raw,a[3],a[4],a[5],alpha)})
    sheets=[]
    for index,rec in enumerate(chunk(202)):
        u=struct.unpack('<27I',rec);f=struct.unpack('<27f',rec)
        atlas=next(a for a in atlases if a['sheetStart']<=index<a['sheetStart']+a['sheetCount'])
        sheets.append({'index':index,'uuid':text(u[0],u[1]),'userData':u[2],'sheetId':u[3],'originalWidth':u[4],'originalHeight':u[5],'offset':list(f[6:8]),'anchor':list(f[8:10]),'uv':list(f[10:18]),'rect':list(u[18:22]),'rotated':bool(u[22]),'atlasId':atlas['id']})
    chunks={}
    for tag in range(203,215):
        rs=chunk(tag)
        if rs:chunks[str(tag)]={'count':len(rs),'stride':len(rs[0]),'recordsHex':[r.hex() for r in rs]}
    # Preserve name/animation fragments for further reconstruction, not a claim
    # of complete animation semantics.
    scenes=[]
    layers=chunk(205);frames=chunk(206)
    by_source={int(s['userData']):s for s in sheets}
    for scene_id,r in enumerate(chunk(204)):
        name=r[:32].split(b'\0')[0].decode('cp949','replace')
        first,count=struct.unpack_from('<II',r,32)
        parsed_layers=[]
        for layer in layers[first:first+count]:
            fs,fc=struct.unpack_from('<II',layer,32);parsed_frames=[]
            for frame in frames[fs:fs+fc]:
                u=struct.unpack('<21I',frame);f=struct.unpack('<21f',frame)
                sheet=by_source.get(u[9])
                parsed_frames.append({'delayMs':u[0],'lifeTimeMs':u[1],'userValue':u[2],'userString':frame[12:36].split(b'\0')[0].decode('cp949','replace'),'sourceSpriteNumber':u[9],'sheetIndex':sheet['index'] if sheet else None,'x':f[10],'y':f[11],'effect':u[12],'grade':f[13],'colorRaw':u[14],'mirrorInterpolRaw':u[15],'scaleX':f[16],'scaleY':f[17],'angle':f[18],'opacity':f[19],'bias':f[20]})
            parsed_layers.append({'name':layer[:32].split(b'\0')[0].decode('cp949','replace'),'frames':parsed_frames})
        scenes.append({'id':scene_id,'name':name,'layers':parsed_layers})
    multis=[];tracks=chunk(208);scene_tracks=chunk(209)
    for multi_id,r in enumerate(chunk(207)):
        first,count=struct.unpack_from('<II',r,32);parsed=[]
        for track in tracks[first:first+count]:
            kind,delay=struct.unpack_from('<II',track);ix=struct.unpack_from('<I',track,40)[0]
            t={'kind':kind,'delayMs':delay,'name':track[8:40].split(b'\0')[0].decode('cp949','replace'),'dataIndex':ix}
            if kind==1 and ix<len(scene_tracks):
                q=scene_tracks[ix];u=struct.unpack('<16I',q);f=struct.unpack('<16f',q)
                t.update({'sceneId':u[0],'x':f[1],'y':f[2],'repeat':u[3],'clipping':u[4],'opacity':f[5],'angle':f[6],'scaleX':f[7],'scaleY':f[8],'lifeTimeMs':u[9],'pivotX':f[10],'pivotY':f[11]})
            parsed.append(t)
        multis.append({'id':multi_id,'name':r[:32].split(b'\0')[0].decode('cp949','replace'),'tracks':parsed})
    return atlases,sheets,{'binaryChunks':chunks,'scenes':scenes,'multiScenes':multis}


def xml(data):
    text=data.decode('cp949').replace('encoding="euc-kr"','encoding="utf-8"')
    root=ET.fromstring(text)
    atlases=[];sheets=[]
    for atlas in root.findall('./AtlasBundle/Atlas'):
        def val(key):return int(atlas.find('_data_'+key).attrib['Value'])
        block=atlas.find('_data_ImageData');b=base64.b64decode(block.text)
        raw=zlib.decompress(b) if block.attrib.get('Compressed')=='1' else b
        w,h,fmt=val('ImageWidth'),val('ImageHeight'),val('PixelFormat')
        aid=val('AtlasID')
        atlases.append({'id':aid,'uuid':atlas.attrib['UUID'],'width':w,'height':h,'pixelFormat':fmt,'image':pixels(raw,w,h,fmt)})
        for s in atlas.findall('./SheetBundle/Sheet'):
            def sv(key):return int(s.find('_data_'+key).attrib['Value'])
            def vec(key):return [float(s.find('_data_'+key).attrib[k]) for k in ['x','y']]
            rect=s.find('_data_rcRegion').attrib
            sheets.append({'index':len(sheets),'uuid':s.attrib['UUID'],'userData':s.attrib.get('UserData'),'sheetId':sv('sheetID'),'originalWidth':sv('orgWidth'),'originalHeight':sv('orgHeight'),'offset':vec('offsetPoint'),'anchor':vec('anchorPoint'),'rect':[int(rect[k]) for k in ['left','top','right','bottom']],'rotated':bool(sv('bRotated')),'atlasId':aid})
    by_source={int(s['userData']):s for s in sheets}
    scenes=[]
    for sid,scene in enumerate(root.findall('./F3XSprAni/XSceneList/XSceneData')):
        layers=[]
        for layer in scene.findall('./XLayerData'):
            frames=[]
            for frame in layer.findall('./XKeyframeData'):
                a=frame.attrib;source=int(a['SprNo']);sheet=by_source.get(source)
                frames.append({'delayMs':int(a['Delay']),'lifeTimeMs':int(a['LifeTime']),'sourceSpriteNumber':source,'sheetIndex':sheet['index'] if sheet else None,'x':float(a['Rx']),'y':float(a['Ry']),'scaleX':float(a['ScaleX']),'scaleY':float(a['ScaleY']),'angle':float(a['RotAngle']),'opacity':float(a['Opacity']),'mirror':int(a['Mirror']),'interpol':int(a['Interpol'])})
            layers.append({'name':layer.attrib['LayerName'],'frames':frames})
        scenes.append({'id':sid,'name':scene.attrib['SceneName'],'layers':layers})
    multis=[]
    for mid,scene in enumerate(root.findall('./F3XSprAni/XMultiSceneList/XMSceneData')):
        tracks=[]
        for track in scene.findall('./XTrackData'):
            t={'kind':int(track.attrib['TrackType']),'delayMs':int(track.attrib['Delay']),'name':track.attrib['TrackName']}
            st=track.find('./XTRACK_SCENE')
            if st is not None:
                a=st.attrib;t.update({'sceneId':int(a['SceneNo']),'x':float(a['PosX']),'y':float(a['PosY']),'repeat':int(a['RepeatCnt']),'opacity':float(a['Alpha']),'angle':float(a['Angle']),'scaleX':float(a['ScaleX']),'scaleY':float(a['ScaleY']),'lifeTimeMs':int(a['LifeTime'])})
            tracks.append(t)
        multis.append({'id':mid,'name':scene.attrib['MSceneName'],'tracks':tracks})
    return atlases,sheets,{'xmlSource':True,'scenes':scenes,'multiScenes':multis}


def render_previews(name,layout,sheets,out):
    by_index={s['index']:s for s in sheets}
    def scene_items(scene_id):
        found=[]
        if scene_id>=len(layout['scenes']):return found
        for layer in layout['scenes'][scene_id]['layers']:
            eligible=[f for f in layer['frames'] if f['delayMs']==0 and f['sheetIndex'] is not None]
            if eligible:found.append(dict(eligible[0]))
        return found
    def multi_items(multi):
        items=[]
        for t in multi['tracks']:
            if t['kind']!=1 or t['delayMs']!=0:continue
            for f in scene_items(t['sceneId']):
                f['x']=t['x']+f['x']*t['scaleX'];f['y']=t['y']+f['y']*t['scaleY']
                f['scaleX']*=t['scaleX'];f['scaleY']*=t['scaleY'];f['opacity']*=t['opacity']
                items.append(f)
        return items
    def draw(items,path,width=1280,height=800,offset=(0,0)):
        canvas=Image.new('RGBA',(width,height),(0,0,0,0))
        for f in items:
            s=by_index[f['sheetIndex']]
            if f['opacity']<=0:continue
            im=Image.open(out/('sheet_'+str(s['index']).zfill(4)+'.png')).convert('RGBA')
            w=max(1,round(im.width*abs(f['scaleX'])));h=max(1,round(im.height*abs(f['scaleY'])))
            if max(w,h)>8192:continue
            im=im.resize((w,h),Image.Resampling.LANCZOS)
            if f['scaleX']<0:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            if f['scaleY']<0:im=im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            if f['opacity']<1:im.putalpha(im.getchannel('A').point(lambda v:int(v*f['opacity'])))
            x=round(offset[0]+f['x'])
            y=round(offset[1]+f['y'])
            canvas.alpha_composite(im,(x,y))
        canvas.save(path)
    if name=='GameBoardBlock':
        board=[]
        for i in range(32):board+=scene_items(i)
        bg=next((m for m in layout['multiScenes'] if m['name']=='game_bg'),None)
        bg_items=multi_items(bg) if bg else []
        draw(bg_items,DOC/'original_board_background.png')
        draw(bg_items,DEST/'board_background.png')
        draw(board,DOC/'original_board_layers.png')
        draw(bg_items+board,DOC/'original_board_composed.png')
        write_json(out/'board-layers.json',{'canvasWidth':1280,'canvasHeight':800,'coordinates':'Positive Y down; top-left pixel placement. Anchors used as rotation pivots, not subtracted from x/y.','items':board})
        bases=[];positions=[]
        for i in range(32):
            fs=scene_items(i)
            if not fs:continue
            f=fs[0];bases.append(f);s=by_index[f['sheetIndex']]
            w=s['originalWidth']*abs(f['scaleX']);h=s['originalHeight']*abs(f['scaleY'])
            positions.append({'index':i,'x':f['x']+w/2,'y':f['y']+h/2,'width':w,'height':h,'left':f['x'],'top':f['y'],'sheetIndex':s['index'],'nameX':fs[1]['x'] if len(fs)>1 else f['x']+w/2,'nameY':fs[1]['y'] if len(fs)>1 else f['y']+h/2})
        draw(bg_items+bases,DEST/'board-base.png')
        write_json(DEST/'board-positions.json',{'canvasWidth':1280,'canvasHeight':800,'coordinates':'Positive Y down native source image. x/y = source tile rectangular center; left/top = source placement. nameX/nameY = raw name layer top-left if present.','tiles':positions})
    if name=='GameBtnDice':
        for target in ['Roll','Roll_s','Odd','Even']:
            multi=next((m for m in layout['multiScenes'] if m['name']==target),None)
            if multi:draw(multi_items(multi),DOC/('original_dice_'+target+'.png'),400,300,(160,130))
    if name=='hud':
        items=[];panels=[]
        for target in ['hud_common_ui','hud_1','hud_2','hud_3','hud_4','hud_btn']:
            multi=next((m for m in layout['multiScenes'] if m['name']==target),None)
            if multi:
                fs=multi_items(multi);items+=fs;panels.append({'name':target,'items':fs})
        draw(items,DOC/'original_hud_4players.png')
        write_json(DEST/'hud-layout.json',{'canvasWidth':1280,'canvasHeight':800,'panels':panels})
    if name=='pop_buy_infor':
        for target in ['popBuyCity','popBuyLandMark','popBuyTourist']:
            multi=next((m for m in layout['multiScenes'] if m['name']==target),None)
            if multi:
                draw(multi_items(multi),DOC/('original_'+target+'.png'))


def run():
    DEST.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
    summary=[]
    with zipfile.ZipFile(APK) as z:
        for name in (sys.argv[2:] or NAMES):
            entry='assets/spr/'+name+'.f3spr';data=z.read(entry)
            atlases,sheets,extra=binary(data) if data.startswith(b'F3ZT_SPR') else xml(data)
            out=DEST/name;out.mkdir(parents=True,exist_ok=True)
            for a in atlases:a['image'].save(out/('atlas_'+str(a['id'])+'.png'))
            crop_images=[]
            for s in sheets:
                a=next(a for a in atlases if a['id']==s['atlasId']);l,t,r,b=s['rect']
                assert 0<=l<=r<a['width'] and 0<=t<=b<a['height'],(name,s)
                crop=a['image'].crop((l,t,r+1,b+1))
                if s['rotated']:crop=crop.transpose(Image.Transpose.ROTATE_270)
                s['file']='sheet_'+str(s['index']).zfill(4)+'.png'
                crop.save(out/s['file']);crop_images.append((s,crop))
            cols=8;cell_w=150;cell_h=150
            pages=math.ceil(len(crop_images)/64)
            contacts=[]
            for page in range(pages):
                subset=crop_images[page*64:(page+1)*64]
                canvas=Image.new('RGB',(cols*cell_w,math.ceil(len(subset)/cols)*cell_h),(42,48,62));draw=ImageDraw.Draw(canvas)
                for j,(s,im) in enumerate(subset):
                    im=im.copy();im.thumbnail((cell_w-10,cell_h-25))
                    x=(j%cols)*cell_w+(cell_w-im.width)//2;y=(j//cols)*cell_h+18
                    canvas.paste(im,(x,y),im)
                    draw.text(((j%cols)*cell_w+5,(j//cols)*cell_h+3),f"id {s['sheetId']}  #{s['index']}",fill='white')
                path=DOC/(name+'_contact_'+str(page)+'.png');canvas.save(path);contacts.append(str(path))
            meta={'sourceEntry':entry,'sourceSha256':hashlib.sha256(data).hexdigest(),'coordinateConvention':'Top-left inclusive atlas rectangle. Crops unrotate packed rectangles 90deg clockwise. Anchors remain source pixels. sheetId is local to atlas; globally unique index used for PNG filenames.','atlases':[{k:v for k,v in a.items() if k!='image'} for a in atlases],'sheets':sheets}
            write_json(out/'metadata.json',meta)
            write_json(DOC/(name+'_structure.json'),extra)
            if 'scenes' in extra:
                layout={'source':entry,'coordinates':'Original positive Y down pixel coordinates. sourceSpriteNumber maps sheets.userData, not sheetId. Scene SprNo and frames parsed statically. Effects/interpolation not fully implemented.','scenes':extra['scenes'],'multiScenes':extra['multiScenes']}
                write_json(out/'original-layout.json',layout)
                render_previews(name,layout,sheets,out)
                if name.startswith('character'):
                    animations={'name':name,'scenes':[{'name':s['name'],'frames':[{'sheetIndex':f['sheetIndex'],'timeMs':f['delayMs'],'durationMs':f['lifeTimeMs']} for layer in s['layers'] for f in layer['frames'] if f['sheetIndex'] is not None]} for s in extra['scenes']]}
                    write_json(out/'animations.json',animations)
            info={'name':name,'atlases':len(atlases),'sprites':len(sheets),'contacts':contacts}
            summary.append(info);print(json.dumps(info))
    # Preserve a complete inventory even when this run selected only a few
    # resources; never replace earlier outputs with a misleading subset count.
    complete=[]
    for path in sorted(DEST.glob('*/metadata.json')):
        meta=json.loads(path.read_text(encoding='utf8'))
        complete.append({'name':path.parent.name,'atlases':len(meta['atlases']),'sprites':len(meta['sheets']),'sourceEntry':meta['sourceEntry'],'sourceSha256':meta['sourceSha256'],'contacts':[str(p) for p in sorted(DOC.glob(path.parent.name+'_contact_*.png'))]})
    write_json(DOC/'decode-summary.json',complete)


if __name__=='__main__':run()
