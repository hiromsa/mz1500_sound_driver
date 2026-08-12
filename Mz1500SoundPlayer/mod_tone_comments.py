file_path_parser = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MultiTrackMmlParser.cs'
with open(file_path_parser, 'r', encoding='utf-8') as f:
    content_parser = f.read()

find = '''    private FmToneData ParseFmToneData(string innerText)
    {
        var tone = new FmToneData();
        var matches = Regex.Matches(innerText, @"-?\d+");'''
repl = '''    private FmToneData ParseFmToneData(string innerText)
    {
        var tone = new FmToneData();
        // Remove comments
        innerText = Regex.Replace(innerText, @";.*", "");
        innerText = Regex.Replace(innerText, @"//.*", "");
        var matches = Regex.Matches(innerText, @"-?\d+");'''

content_parser = content_parser.replace(find, repl)

find_env = '''    private EnvelopeData ParseEnvelopeData(string innerText, bool allowNegative = false)
    {
        string pattern = allowNegative ? @"-?\d+(?:\s*[xX]\s*\d+)?|\||>" : @"\d+(?:\s*[xX]\s*\d+)?|\||>";
        var matches = Regex.Matches(innerText, pattern);'''
repl_env = '''    private EnvelopeData ParseEnvelopeData(string innerText, bool allowNegative = false)
    {
        innerText = Regex.Replace(innerText, @";.*", "");
        innerText = Regex.Replace(innerText, @"//.*", "");
        string pattern = allowNegative ? @"-?\d+(?:\s*[xX]\s*\d+)?|\||>" : @"\d+(?:\s*[xX]\s*\d+)?|\||>";
        var matches = Regex.Matches(innerText, pattern);'''

content_parser = content_parser.replace(find_env, repl_env)

with open(file_path_parser, 'w', encoding='utf-8') as f:
    f.write(content_parser)