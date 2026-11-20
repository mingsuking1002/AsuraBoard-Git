using System;
namespace Marble.Core
{
    // Stable compact appearance code shared by profile saves, replays, and Steam rosters.
    public static class WardrobeCatalog
    {
        public const int DefaultCode=(1<<4)|(1<<11)|(1<<13)|(1<<15);
        public static readonly string[] Categories={"성별·피부","헤어","모자","상의","바지","신발","악세서리","주사위"};
        static readonly int[] Shifts={0,1,4,8,11,13,15,17};
        static readonly int[] Bits={1,3,4,3,2,2,2,3};
        public static readonly int[] Counts={2,6,11,5,3,3,3,6};
        public static int Get(int code,int field)=>(code>>Shifts[field])&((1<<Bits[field])-1);
        public static int Set(int code,int field,int value)
        {
            if(field<0 || field>=Counts.Length || value<0 || value>=Counts[field])throw new ArgumentOutOfRangeException();
            int mask=((1<<Bits[field])-1)<<Shifts[field];return(code&~mask)|(value<<Shifts[field]);
        }
        public static bool IsValid(int code)
        {
            if(code<0 || code>=(1<<20))return false;
            for(int i=0;i<Counts.Length;i++)if(Get(code,i)>=Counts[i])return false;
            return true;
        }
    }
}
