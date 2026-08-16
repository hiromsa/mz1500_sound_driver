import io

def process(f, replacements):
    with io.open(f, 'r', encoding='utf-8') as file:
        content = file.read()
    for o, n in replacements:
        content = content.replace(o, n)
    with io.open(f, 'w', encoding='utf-8', newline='') as file:
        file.write(content)

repl1 = [
    ('ChkA', 'ChkP1'), ('ChkB', 'ChkP2'), ('ChkC', 'ChkP3'),
    ('ChkD', 'ChkN1'), ('ChkE', 'ChkP4'), ('ChkF', 'ChkP5'),
    ('ChkG', 'ChkP6'), ('ChkH', 'ChkN2'), ('ChkP', 'ChkB1'),
    ('CurrentVolumeA', 'CurrentVolumeP1'), ('CurrentVolumeB', 'CurrentVolumeP2'),
    ('CurrentVolumeC', 'CurrentVolumeP3'), ('CurrentVolumeD', 'CurrentVolumeN1'),
    ('CurrentVolumeE', 'CurrentVolumeP4'), ('CurrentVolumeF', 'CurrentVolumeP5'),
    ('CurrentVolumeG', 'CurrentVolumeP6'), ('CurrentVolumeH', 'CurrentVolumeN2'),
    ('CurrentVolumeP', 'CurrentVolumeB1'),
    ('Text=\"A\"', 'Text=\"P1\"'), ('Text=\"B\"', 'Text=\"P2\"'), ('Text=\"C\"', 'Text=\"P3\"'),
    ('Text=\"D\"', 'Text=\"N1\"'), ('Text=\"E\"', 'Text=\"P4\"'), ('Text=\"F\"', 'Text=\"P5\"'),
    ('Text=\"G\"', 'Text=\"P6\"'), ('Text=\"H\"', 'Text=\"N2\"'), ('Text=\"P\"', 'Text=\"B1\"')
]
process('Mz1500SoundPlayer/MainWindow.axaml', repl1)

repl2 = repl1 + [
    ('_currentVolumeA', '_currentVolumeP1'), ('_currentVolumeB', '_currentVolumeP2'),
    ('_currentVolumeC', '_currentVolumeP3'), ('_currentVolumeD', '_currentVolumeN1'),
    ('_currentVolumeE', '_currentVolumeP4'), ('_currentVolumeF', '_currentVolumeP5'),
    ('_currentVolumeG', '_currentVolumeP6'), ('_currentVolumeH', '_currentVolumeN2'),
    ('_currentVolumeP', '_currentVolumeB1'),
    ('volumes[\"A\"]', 'volumes[\"P1\"]'), ('volumes[\"B\"]', 'volumes[\"P2\"]'),
    ('volumes[\"C\"]', 'volumes[\"P3\"]'), ('volumes[\"D\"]', 'volumes[\"N1\"]'),
    ('volumes[\"E\"]', 'volumes[\"P4\"]'), ('volumes[\"F\"]', 'volumes[\"P5\"]'),
    ('volumes[\"G\"]', 'volumes[\"P6\"]'), ('volumes[\"H\"]', 'volumes[\"N2\"]'),
    ('volumes[\"P\"]', 'volumes[\"B1\"]'),
    ('activeChannels.Add(\"A\")', 'activeChannels.Add(\"P1\")'),
    ('activeChannels.Add(\"B\")', 'activeChannels.Add(\"P2\")'),
    ('activeChannels.Add(\"C\")', 'activeChannels.Add(\"P3\")'),
    ('activeChannels.Add(\"D\")', 'activeChannels.Add(\"N1\")'),
    ('activeChannels.Add(\"E\")', 'activeChannels.Add(\"P4\")'),
    ('activeChannels.Add(\"F\")', 'activeChannels.Add(\"P5\")'),
    ('activeChannels.Add(\"G\")', 'activeChannels.Add(\"P6\")'),
    ('activeChannels.Add(\"H\")', 'activeChannels.Add(\"N2\")'),
    ('activeChannels.Add(\"P\")', 'activeChannels.Add(\"B1\")')
]
process('Mz1500SoundPlayer/MainWindow.axaml.cs', repl2)
