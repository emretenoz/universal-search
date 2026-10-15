using System.Runtime.InteropServices;
using UniversalSearch.Core;
using UniversalSearch.Infrastructure;

namespace UniversalSearch.App;
public static class ApplicationCatalog
{
    static IReadOnlyDictionary<string,string> identities = new Dictionary<string,string>();
    public static IReadOnlyList<SearchEntry> Collapse(IEnumerable<SearchEntry> entries) => ApplicationResults.Collapse(entries, Volatile.Read(ref identities));
    public static Task RefreshAsync(SearchStore store, Configuration config)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            object? shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
                folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
                if (folder == null) { completion.SetResult(); return; }
                items = ((dynamic)folder).Items();
                var aliases = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                using var db = store.Open(); using var tx = db.BeginTransaction();
                using (var clear = db.CreateCommand()) { clear.CommandText = "DELETE FROM entries WHERE path LIKE 'shell:AppsFolder\\%'"; clear.ExecuteNonQuery(); }
                for (int i = 0; i < (int)((dynamic)items).Count; i++)
                {
                    object? item = null;
                    try
                    {
                        item = ((dynamic)items).Item(i);
                        string id = (string)((dynamic)item).ExtendedProperty("System.AppUserModel.ID");
                        string name = (string)((dynamic)item).Name;
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            var path = "shell:AppsFolder\\" + id;
                            store.Upsert(db,new(path,name,EntryKind.Application));
                            aliases[path] = id;
                        }
                    }
                    catch (Exception ex) { config.Log(ex); }
                    finally { if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }
                }
                tx.Commit();
                // Shortcut metadata is read on this STA worker; never merge on display name alone.
                foreach (var root in FileIndexer.StartMenuRoots.Where(Directory.Exists))
                {
                    foreach (var path in Directory.EnumerateFiles(root,"*.lnk",new EnumerationOptions { RecurseSubdirectories=true, IgnoreInaccessible=true, AttributesToSkip=FileAttributes.ReparsePoint }))
                    {
                        object? parent = null, shortcut = null;
                        try
                        {
                            parent = ((dynamic)shell!).NameSpace(Path.GetDirectoryName(path));
                            shortcut = ((dynamic)parent!).ParseName(Path.GetFileName(path));
                            var id = Convert.ToString(((dynamic)shortcut!).ExtendedProperty("System.AppUserModel.ID"));
                            var arguments = Convert.ToString(((dynamic)shortcut!).ExtendedProperty("System.Link.Arguments"));
                            if (!string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(arguments)
                                && aliases.ContainsKey("shell:AppsFolder\\" + id)) aliases[path] = id;
                        }
                        catch (Exception ex) { config.Log(ex); }
                        finally { foreach(var obj in new[]{shortcut,parent}) if(obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
                    }
                }
                Volatile.Write(ref identities, aliases);
                completion.SetResult();
            }
            catch (Exception ex) { config.Log(ex); completion.SetResult(); }
            finally
            {
                foreach (var obj in new[] { items,folder,shell }) if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj);
            }
        }) { IsBackground = true, Name = "Application catalog" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
