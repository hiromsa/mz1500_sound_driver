file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MultiTrackSequenceProvider.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

find = '''    public HashSet<string> ActiveChannels { get; set; } = new HashSet<string>(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "P" });'''
repl = '''    public HashSet<string> ActiveChannels { get; set; } = new HashSet<string>(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "P", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" });'''

content = content.replace(find, repl)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)