import re

with open('Mz1500SoundPlayer/Sound/MmlSequenceProvider.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# Add _lastLength
code = code.replace('private int _waitFrames = 0;', 'private int _waitFrames = 0;\n    private int _lastLength = 0;')

# Replace Reset
reset_old = '''    public void Reset()
    {
        _pc = 0;
        _hwVolume = 15;
        _hwFreqRaw = 0;
        _phase = 0;
        _phaseIncrement = 0;
        
        _isNoiseMode = false;
        _noiseFeedback = 0;
        _lfsr = 0x4000;
        
        _waitFrames = 0;
        _isEnd = false;
        _isRest = false;
        _loopOffsetPc = -1;'''

reset_new = '''    public void Reset()
    {
        _pc = 0;
        _hwVolume = 15;
        _hwFreqRaw = 0;
        _phase = 0;
        _phaseIncrement = 0;
        
        _isNoiseMode = false;
        _noiseFeedback = 0;
        _lfsr = 0x4000;
        
        _waitFrames = 0;
        _lastLength = 0;
        _isEnd = false;
        _isRest = false;
        _loopOffsetPc = -1;'''

code = code.replace(reset_old, reset_new)

with open('Mz1500SoundPlayer/Sound/MmlSequenceProvider.cs', 'w', encoding='utf-8') as f:
    f.write(code)
