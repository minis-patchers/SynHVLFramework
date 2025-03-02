using System.Text;

namespace SynPatcher.Types.Ini;
class IniFile : IDisposable
{
    string iniFile;
    Dictionary<string, string> dict;
    public IniFile(string inifile)
    {
        iniFile = inifile;
        dict = File.ReadLines(iniFile)
               .Where(line => !string.IsNullOrWhiteSpace(line))
               .Where(line => !line.Trim().StartsWith(';'))
               .Where(line => !line.Trim().StartsWith('#'))
               .Select(line => line.Split('=', 2))
               .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }
    public bool KeyExists(string Key) {
        return dict.ContainsKey(Key);
    }
    public string Read(string Key) {
        return dict[Key];
    }
    public void Write(string Key, string value) {
        dict[Key] = value;
    }
    public void Dispose() {
        StringBuilder sb = new();
        foreach(var data in dict.OrderBy(x=>x.Key)) {
            sb.AppendLine($"{data.Key} = {data.Value}");
        }
        File.WriteAllText(iniFile, sb.ToString());
    }
}