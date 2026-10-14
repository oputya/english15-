using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public sealed class UserSettings {
    public string Application { get; set; }
    public int Version { get; set; }
    public string Provider { get; set; }
    public string Model { get; set; }
    public string Question { get; set; }
    public string EncryptedApiKey { get; set; }
    static readonly byte[] entropy=Encoding.UTF8.GetBytes("DiscordHelper user settings v1");
    public static string PathName { get { return Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),"settings.json"); } }
    public static bool IsOwned(string path) {
        try { var settings=new JavaScriptSerializer().Deserialize<UserSettings>(File.ReadAllText(path)); return settings!=null && settings.Application=="DiscordHelper.Settings" && settings.Version==1; } catch { return false; }
    }
    public static UserSettings Load(string path) {
        if(!File.Exists(path)) return null;
        var settings=new JavaScriptSerializer().Deserialize<UserSettings>(File.ReadAllText(path));
        if(settings==null || settings.Application!="DiscordHelper.Settings" || settings.Version!=1) throw new IOException("settings.json не является настройками приложения.");
        ModelApi.BaseUrl(settings.Provider); return settings;
    }
    public string ReadKey() {
        if(String.IsNullOrEmpty(EncryptedApiKey)) return "";
        byte[] clear=ProtectedData.Unprotect(Convert.FromBase64String(EncryptedApiKey),entropy,DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(clear); } finally { Array.Clear(clear,0,clear.Length); }
    }
    public static void Save(string path,string provider,string model,string question,string key) {
        ModelApi.BaseUrl(provider);
        if(File.Exists(path) && !IsOwned(path)) throw new IOException("settings.json не является настройками приложения; файл не перезаписан.");
        string encrypted=""; byte[] clear=Encoding.UTF8.GetBytes(key??"");
        try { if(clear.Length>0) encrypted=Convert.ToBase64String(ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser)); }
        finally { Array.Clear(clear,0,clear.Length); }
        var settings=new UserSettings { Application="DiscordHelper.Settings",Version=1,Provider=provider,Model=model??"",Question=question??"",EncryptedApiKey=encrypted };
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(settings),Encoding.UTF8); if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
