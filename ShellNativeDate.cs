namespace SprocketShellSelector;
// TechDate supports year zero; System.DateTime does not.
internal readonly record struct ShellNativeDate(int Year,int Month,int Day)
{
    internal static readonly ShellNativeDate Maximum=new(9999,12,31);
    internal int Key=>Year*10000+Month*100+Day;
    internal bool Valid
    {
        get
        {
            if(Year<0||Year>9999||Month<1||Month>12||Day<1)return false;
            return Day<=DateTime.DaysInMonth(Year==0?400:Year,Month);
        }
    }
    public static bool operator <(ShellNativeDate a,ShellNativeDate b)=>a.Key<b.Key;
    public static bool operator >(ShellNativeDate a,ShellNativeDate b)=>a.Key>b.Key;
    public static bool operator <=(ShellNativeDate a,ShellNativeDate b)=>a.Key<=b.Key;
    public static bool operator >=(ShellNativeDate a,ShellNativeDate b)=>a.Key>=b.Key;
}
