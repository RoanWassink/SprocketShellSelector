namespace SprocketShellSelector;
// Remembers only the ownership keys needed for scoped reset. Spent authority stays in EraCells.
internal sealed class PlacedEraState
{
    private readonly HashSet<EraCell> owned=new();
    internal static EraCell Key(long spawn,int element)=>new(spawn,element,0,0,0);
    internal void Track(EraCell cell)=>owned.Add(cell);
    internal bool Spent(EraCells shared,long spawn,int element)=>shared.Contains(Key(spawn,element));
    internal void Reset(EraCells shared)
    {foreach(var cell in owned)shared.RemoveElement(cell.Spawn,cell.Element);owned.Clear();}
    internal void RemoveSpawn(long spawn)=>owned.RemoveWhere(c=>c.Spawn==spawn);
    internal void RemoveElement(long spawn,int element)=>owned.RemoveWhere(c=>c.Spawn==spawn&&c.Element==element);
}
