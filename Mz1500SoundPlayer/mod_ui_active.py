file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\MainWindow.axaml.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

find = '''        if (ChkP.IsChecked == true) activeChannels.Add("P");'''
repl = '''        if (ChkP.IsChecked == true) activeChannels.Add("P");
        if (ChkAll.IsChecked == true) 
        {
            activeChannels.Add("F1"); activeChannels.Add("F2"); activeChannels.Add("F3"); activeChannels.Add("F4");
            activeChannels.Add("F5"); activeChannels.Add("F6"); activeChannels.Add("F7"); activeChannels.Add("F8");
        }'''

content = content.replace(find, repl)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)