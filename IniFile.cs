using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public class IniFile
{
    private readonly string _path;
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    public IniFile(string path)
    {
        _path = Path.GetFullPath(path);
        _sections = ParseIniFile();
    }

    private Dictionary<string, Dictionary<string, string>> ParseIniFile()
    {
        var data = new Dictionary<string, Dictionary<string, string>>(System.StringComparer.OrdinalIgnoreCase);
        string currentSection = null;

        if (File.Exists(_path))
        {
            var lines = File.ReadAllLines(_path, Encoding.UTF8); 
            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();

               
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    if (!data.ContainsKey(currentSection))
                        data[currentSection] = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                
                if (currentSection != null)
                {
                    int eqIndex = line.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string key = line.Substring(0, eqIndex).Trim();
                        string value = line.Substring(eqIndex + 1).Trim();
                        data[currentSection][key] = value;
                    }
                }
            }
        }
        return data;
    }

    public string Read(string section, string key)
    {
        if (_sections.TryGetValue(section, out var sectionData) &&
            sectionData.TryGetValue(key, out var value))
        {
            return value;
        }
        return string.Empty;
    }

    public List<string> GetSections() => new List<string>(_sections.Keys);
}