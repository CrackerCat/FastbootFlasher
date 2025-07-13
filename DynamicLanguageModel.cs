using System.ComponentModel;
using System.Dynamic;
using System.Runtime.CompilerServices;

public class DynamicLanguageModel : DynamicObject, INotifyPropertyChanged
{
    private readonly IniFile _ini;
    private string _currentLang;
    private HashSet<string> _cachedKeys = new();

    public DynamicLanguageModel(string iniPath)
    {
        _ini = new IniFile(iniPath);
        _currentLang = "zh"; // 默认语言
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void SetLanguage(string lang)
    {
        _currentLang = lang;

        foreach (var key in _cachedKeys)
        {
            OnPropertyChanged(key);
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        string key = binder.Name;
        _cachedKeys.Add(key);

        result = _ini.Read(_currentLang, key);
        return true;
    }

    public List<string> AvailableLanguages => _ini.GetSections();
}