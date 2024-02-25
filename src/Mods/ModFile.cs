using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CementTools;

// helper class which stores information about a certain field in a mod file: the value, and the attributes
public class ModFileValue
{
    public string value;
    public readonly string[] attributes;

    public ModFileValue(string value, string[] attributes)
    {
        this.value = value;
        this.attributes = attributes;
    }

    public bool HasAttribute(string attributeName)
    {   
        foreach (string att in attributes)
        {
            if (att == attributeName)
            {
                return true;
            }
        }

        return false;
    }
}

// class which parses mod files into memory, and provides useful methods for working with them
public class ModFile
{
    private struct ModFileParameter
    {
        public string[] attributes;
        public string key;
        public string value;
    }

    public Dictionary<string, ModFileValue> Values {
        get 
        {
            return _values; 
        } 
        private set 
        { 
            _values = value; 
        }
    }

    public ModFile[] RequiredMods { 
        private get
        {
            return _requiredMods;
        }
        set
        {
            _requiredMods = value;
        }
    }

    public List<ModFile> RequiredBy
    {
        private get
        {
            return _requiredBy;
        }
        set
        {
            _requiredBy = value;
        }
    }

    public event Action ChangedValues;

    private Dictionary<string, ModFileValue> _values = new Dictionary<string, ModFileValue>();
    private static readonly Dictionary<string, ModFile> _modFiles = new Dictionary<string, ModFile>();
    private ModFile[] _requiredMods;
    private List<ModFile> _requiredBy = new List<ModFile>();

    public ModFile[] requiredMods => _requiredMods;
    public ModFile[] requiredBy => _requiredBy.ToArray();

    private bool _isBad = false;

    // used to check if a mod was flagged as bad. Check FlagAsBad method.
    public bool IsBad
    {
        get
        {
            return _isBad;
        }
        private set {
            _isBad = value;
        }
    }

    private bool _isLoaded = false;

    public bool IsLoaded
    {
        get
        {
            return _isLoaded;
        }
    }

    public static ModFile[] All
    {
        get
        {
            ModFile[] files = new ModFile[_modFiles.Count];

            int i = 0;
            foreach (ModFile file in _modFiles.Values)
            {
                files[i] = file;
                i++;
            }

            return files;
        }
    }

    public readonly string path;

    private ModFile(string path)
    {
        this.path = path;
        Reload(false);
    }

    // each mod file has only 1 instance, so constructing a mod file isn't allowed
    // ModFile.Get, checks if the modfile has already been loaded. if it has it returns a reference to that instance,
    // if it hasn't it loads the mod file and stores a reference to it in a dictionary, with its path as a key
    public static ModFile Get(string path)
    {
        if (_modFiles.ContainsKey(path))
        {
            return _modFiles[path];
        }
        _modFiles[path] = new ModFile(path);
        return _modFiles[path];
    }

    // Used to show Cement that if a mod was badly formatted or failed to load previously, it isn't used again by other steps
    public void FlagAsBad()
    {
        IsBad = true;
    }

    public void GotLoaded()
    {
        _isLoaded = true;
    }

    public void AddRequiredBy(ModFile file)
    {
        _requiredBy.Add(file);
    }

    public void InvokeChangedValues()
    {
        if (ChangedValues != null)
        {
            try
            {
                 ChangedValues.Invoke();
            }
            catch (Exception e)
            {
                Cement.Log($"ERROR OCCURRED IN CHANGED VALUES: {e}");
            }
           
        }
    }

    public ModFileValue GetValue(string key)
    {
        if (!_values.ContainsKey(key))
        {
            return null;
        }

        return _values[key];
    }

    private void SetValue(string key, ModFileValue value)
    {
        _values[key] = value;
    }

    public string GetString(string key)
    {
        ModFileValue value = GetValue(key);
        if (value == null)
        {
            return null;
        }
        return value.value.Trim();
    }

    public void SetString(string key, string value)
    {
        if (_values.ContainsKey(key))
        {
            _values[key].value = value;
        }
        else
        {
            SetValue(key, new ModFileValue(value, new string[] {}));
        }
    }

    public float GetFloat(string key)
    {
        string b = GetString(key);
        if (b == null)
        {
            return 0.0f;
        }
        return float.Parse(b);
    }

    public void SetFloat(string key, float value)
    {
        SetString(key, value.ToString());
    }

    public int GetInt(string key)
    {
        string b = GetString(key);
        if (b == null)
        {
            return 0;
        }
        return int.Parse(b);
    }

    public void SetInt(string key, int value)
    {
        SetString(key, value.ToString());
    }

    public bool GetBool(string key)
    {
        string b = GetString(key);
        if (b == null)
        {
            return false;
        }
        return b == "true";
    }

    public void SetBool(string key, bool value)
    {
        if (value)
        {
            SetString(key, "true");
        }
        else
        {
            SetString(key, "false");
        }
    }

    // writes the new field names and values, and attributes to the mod file
    public void UpdateFile()
    {
        StringWriter writer = new StringWriter();
        Dictionary<string, ModFileValue>.KeyCollection.Enumerator enumerator = _values.Keys.GetEnumerator();
        for (int i = 0; i < _values.Count; i++)
        {
            enumerator.MoveNext();
            string key = enumerator.Current;
            foreach (string attribute in _values[key].attributes)
            {
                writer.Write("[" + attribute + "] ");
            }
            writer.Write(key + "=" + _values[key].value);

            if (i != _values.Count - 1)
            {
                writer.WriteLine();
            }
        }

        File.WriteAllText(path, writer.ToString());
    }

    // code for actually parsing the parameters
    private ModFileParameter[] GetParameters(string[] lines)
    {
        List<ModFileParameter> modFileParameters = new List<ModFileParameter>();

        for (int i = 0; i < lines.Length; i++)
        {   
            string line = lines[i].Trim();

            if (line == "")
            {
                continue;
            }

            string key = "";
            string value = "";
            bool addingKey = true;
            bool removeWhitespace = true;
            List<string> attributes = new List<string>();

            for (int j = 0; j < line.Length; j++)
            {
                char currentChar = line[j];

                if (removeWhitespace && char.IsWhiteSpace(currentChar))
                {
                    continue;
                }

                if (currentChar == '[' && addingKey)
                {
                    string currentAttribute = "";
                    j++;
                    currentChar = line[j];
                    while (currentChar != ']')
                    {   
                        if (!char.IsWhiteSpace(currentChar))
                            currentAttribute += currentChar;

                        j++;
                        if (j >= line.Length)
                        {
                            break;
                        }
                        currentChar = line[j];
                    }
                    CementTools.Cement.Log($"ADDING ATTRIBUTE: {currentAttribute}");
                    attributes.Add(currentAttribute);
                    continue;
                }

                removeWhitespace = false;

                if (addingKey)
                {
                    if (currentChar == '=')
                    {
                        addingKey = false;
                        continue;
                    }
                    key += currentChar;
                }
                else
                {
                    value += currentChar;
                }
            }

            if (key == "")
            {
                continue;
            }

            ModFileParameter parameter = new ModFileParameter
            {
                value = value,
                key = key,
                attributes = attributes.ToArray()
            };
            modFileParameters.Add(parameter);
        }

        Cement.Log("Got params");
        return modFileParameters.ToArray();
    }

    // reloads the mod file into memory.
    // if clearExistingValues is true, it will first clear all the old mod file values
    public void Reload(bool clearExistingValues = true)
    {
        if (clearExistingValues)
        {
            _values.Clear();
        }
        
        ModFileParameter[] parameters = GetParameters(File.ReadAllLines(path));
        foreach (ModFileParameter parameter in parameters)
        {
            _values[parameter.key] = new ModFileValue(parameter.value, parameter.attributes);
        }
    }
}