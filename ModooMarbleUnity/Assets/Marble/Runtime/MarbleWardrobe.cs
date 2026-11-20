using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    public sealed partial class MarbleApp
    {
        int wardrobeTab,wardrobeSavedOutfit=WardrobeCatalog.DefaultCode,wardrobeSavedColor,wardrobeSavedDice,wardrobeSavedAvatar;
        string wardrobeSavedName;float wardrobeYaw=-18;
        void OpenWardrobe()
        {
            wardrobeSavedOutfit=profileOutfit;wardrobeSavedColor=profileColor;wardrobeSavedDice=profileDiceSkin;wardrobeSavedAvatar=profileAvatar;wardrobeSavedName=localName;
            wardrobeTab=0;wardrobeYaw=-18;flow.Navigate(PlayScreen.Customize);
        }
        void CancelWardrobe()
        {
            profileOutfit=wardrobeSavedOutfit;profileColor=wardrobeSavedColor;profileDiceSkin=wardrobeSavedDice;profileAvatar=wardrobeSavedAvatar;
            if(wardrobeSavedName!=null)localName=wardrobeSavedName;RefreshMenuModel();flow.Navigate(PlayScreen.Main);
        }
        void PickWardrobe(int field,int value)
        {profileOutfit=WardrobeCatalog.Set(profileOutfit,field,value);RefreshMenuModel();menuModels.SetPreviewYaw(wardrobeYaw);}
        void DrawWardrobe()
        {
            PaperTheme.Card(new Rect(90,120,1260,710),PaperTheme.Paper,true,3);
            GUI.Label(new Rect(125,142,700,42),"내 옷장",title);
            GUI.Label(new Rect(127,188,650,25),"입어 보고, 돌려 보고, 마음에 들면 저장해요.",small);
            PaperTheme.Card(new Rect(126,230,355,474),PaperTheme.Wash(MarbleBoardView.PlayerColors[profileColor],.16f),false,2.3f);
            PaperTheme.Chip(new Rect(144,244,119,28),PaperTheme.Paper);
            CircuitArt.Label(new Rect(148,245,111,26),"미리보기",font,13,ink,true);
            if(menuModels!=null)GUI.DrawTexture(new Rect(147,239,313,390),menuModels.Portrait(0),ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(151,637,302,25),WardrobeCatalog.Get(profileOutfit,0)==0?"남성형 캐릭터":"여성형 캐릭터",center);
            if(Button(new Rect(151,670,86,30),"↶ 회전",muted)){wardrobeYaw-=30;menuModels.SetPreviewYaw(wardrobeYaw);}
            if(Button(new Rect(249,670,108,30),"정면",muted)){wardrobeYaw=0;menuModels.SetPreviewYaw(0);}
            if(Button(new Rect(369,670,86,30),"회전 ↷",muted)){wardrobeYaw+=30;menuModels.SetPreviewYaw(wardrobeYaw);}
            GUI.Label(new Rect(520,157,140,25),"플레이어 이름",small);localName=PaperInput(new Rect(664,150,639,40),localName,24,label);
            for(int i=0;i<WardrobeCatalog.Categories.Length;i++)
                if(Button(new Rect(520+(i%4)*199,225+(i/4)*46,187,36),WardrobeCatalog.Categories[i],wardrobeTab==i?dark:muted))wardrobeTab=i;
            PaperTheme.Card(new Rect(520,329,783,375),PaperTheme.Wash(PaperTheme.Table,.08f),false,1.8f);
            if(wardrobeTab==0)
            {
                GUI.Label(new Rect(546,350,700,27),"성별",label);
                for(int i=0;i<2;i++)if(Button(new Rect(546+i*215,389,198,45),(WardrobeCatalog.Get(profileOutfit,0)==i?"✓ ":"")+(i==0?"남성":"여성"),WardrobeCatalog.Get(profileOutfit,0)==i?dark:muted))PickWardrobe(0,i);
                GUI.Label(new Rect(546,466,700,27),"피부색",label);
                for(int i=0;i<SidekickAvatar.SkinHex.Length;i++)
                {
                    var rect=new Rect(546+i*113,511,95,76);var color=MarbleBoardView.Hex(SidekickAvatar.SkinHex[i]);
                    PaperTheme.Card(rect,color,false,WardrobeCatalog.Get(profileOutfit,1)==i?3.6f:1.2f);
                    if(WardrobeCatalog.Get(profileOutfit,1)==i)CircuitArt.Label(rect,"✓",font,24,PaperTheme.Ink,true);
                    if(GUI.Button(rect,GUIContent.none,GUIStyle.none))PickWardrobe(1,i);
                }
                GUI.Label(new Rect(546,631,700,25),"성별과 피부색은 의상 선택과 별도로 적용됩니다.",small);
            }
            else if(wardrobeTab==7)DrawWardrobeDice();
            else
            {
                int field=wardrobeTab+1;
                string[] names=field==2?SidekickAvatar.HairNames:field==3?SidekickAvatar.HatNames:field==7?SidekickAvatar.AccessoryNames:SidekickAvatar.ClothingNames;
                GUI.Label(new Rect(546,350,700,27),WardrobeCatalog.Categories[wardrobeTab]+" 선택",label);
                for(int i=0;i<names.Length;i++)
                {
                    bool selected=WardrobeCatalog.Get(profileOutfit,field)==i;
                    if(Button(new Rect(546+(i%3)*242,394+(i/3)*66,226,51),(selected?"✓ ":"")+names[i],selected?dark:muted))PickWardrobe(field,i);
                }
                GUI.Label(new Rect(546,669,700,25),"선택하면 왼쪽 캐릭터에 바로 입혀집니다.",small);
            }
            if(Button(new Rect(126,744,355,46),"기본 착장으로",muted)){profileOutfit=WardrobeCatalog.DefaultCode;wardrobeYaw=-18;RefreshMenuModel();}
            if(Button(new Rect(520,744,377,46),"저장하고 메인으로",dark))SaveProfile();
            if(Button(new Rect(915,744,388,46),"취소",muted))CancelWardrobe();
        }
        void DrawWardrobeDice()
        {
            GUI.Label(new Rect(546,348,700,28),"주사위 외형 · 도전과제",label);
            for(int i=0;i<CosmeticCatalog.DiceSkinCount;i++)
                if(Button(new Rect(546+(i%3)*242,391+(i/3)*58,226,45),(achievements.CanEquipDice(i)?"":"잠김 · ")+CosmeticCatalog.DiceNames[i],profileDiceSkin==i?dark:muted,achievements.CanEquipDice(i))){profileDiceSkin=i;menuModels.SetDiceSkin(i);}
            GUI.Label(new Rect(546,516,700,27),"플레이어 색상",label);
            for(int i=0;i<4;i++)if(Button(new Rect(546+i*180,554,165,43),new[]{"파랑","코랄","초록","노랑"}[i],MarbleBoardView.PlayerColors[i])){profileColor=i;RefreshMenuModel();menuModels.SetPreviewYaw(wardrobeYaw);}
            if(menuModels!=null)GUI.DrawTexture(new Rect(550,612,220,75),menuModels.DiceTexture,ScaleMode.ScaleToFit,true);
            if(Button(new Rect(805,628,466,41),"도전과제 · 해금 조건",muted))achievementSheet=true;
        }
    }
}
