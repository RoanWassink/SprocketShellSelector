using System.Text;
namespace SprocketJsonEditor;
public sealed record JsonFileChange(string Path,string? Original,string Updated);
/// <summary>Conflict detection, backups, atomic per-file replacement and rollback on partial failure.</summary>
public static class JsonFileTransaction
{
    public static void Commit(IEnumerable<JsonFileChange> changes)
    {
        var all=changes.ToArray();
        var list=all.Where(c=>c.Original!=c.Updated).ToArray();
        if(list.Select(c=>System.IO.Path.GetFullPath(c.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=list.Length)throw new IOException("Duplicate transaction target.");
        foreach(var c in all)
            if(c.Original==null?File.Exists(c.Path):!File.Exists(c.Path)||File.ReadAllText(c.Path)!=c.Original)
                throw new IOException("File changed while editing: "+System.IO.Path.GetFileName(c.Path)+". Cancel and reopen.");
        var committed=new List<(JsonFileChange Change,string? Backup)>();
        try
        {
            foreach(var c in list)
            {
                var temp=c.Path+".editor-"+Guid.NewGuid().ToString("N")+".tmp";
                var backup=c.Original==null?null:c.Path+".backup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(c.Path)!);
                    File.WriteAllText(temp,c.Updated,new UTF8Encoding(false));
                    if(c.Original==null)File.Move(temp,c.Path);else File.Replace(temp,c.Path,backup);
                    committed.Add((c,backup));
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
            }
        }
        catch
        {
            foreach(var c in committed.AsEnumerable().Reverse())
                if(c.Backup==null)File.Delete(c.Change.Path);else File.Copy(c.Backup,c.Change.Path,true);
            throw;
        }
    }
}

