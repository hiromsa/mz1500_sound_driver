file_path = r'c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\MultiTrackSequenceProvider.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

vol_find = '''        foreach (var item in _trackProviders)
        {
            vols[item.TrackName] = item.Provider.CurrentVolume;
        }'''

vol_repl = '''        foreach (var item in _trackProviders)
        {
            if (item.Provider is MmlSequenceProvider m) vols[item.TrackName] = m.CurrentVolume;
            else vols[item.TrackName] = 0;
        }'''

content = content.replace(vol_find, vol_repl)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)