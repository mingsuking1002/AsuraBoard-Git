using System;
using Marble.Core;
using Marble.Online;
internal static class LoopSourceRunner
{
    static int Main()
    {
        try
        {
            Console.WriteLine("core="+CoreSelfTests.Run());Console.WriteLine("construction="+ConstructionSelfTests.Run());
            Console.WriteLine("augment="+AugmentSelfTests.Run());Console.WriteLine("loop="+PlayLoopSelfTests.Run());
            Console.WriteLine("online="+OnlineSelfTests.Run());Console.WriteLine("progression="+ProgressionSelfTests.Run());Console.WriteLine("bodyEconomy="+BodyEconomySelfTests.Run());Console.WriteLine("voiceSettings="+VoiceSettingsSelfTests.Run());Console.WriteLine("abilityModules="+AbilityModuleSelfTests.Run());Console.WriteLine("specialMechanics="+SpecialMechanicsSelfTests.Run());Console.WriteLine("uiFramework="+UiFrameworkSelfTests.Run());Console.WriteLine("diamondBoard="+DiamondBoardLayoutSelfTests.Run());Console.WriteLine("movement="+MovementSelfTests.Run());Console.WriteLine("diceThrow="+DiceThrowMotionSelfTests.Run());return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
