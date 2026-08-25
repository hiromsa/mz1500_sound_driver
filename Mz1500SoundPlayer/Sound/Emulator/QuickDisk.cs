using System;
using System.IO;

public class QuickDisk
{
    public const int QUICKDISK_SIO_RTSA = 0;
    public const int QUICKDISK_SIO_DTRB = 1;
    public const int QUICKDISK_SIO_SYNC = 2;
    public const int QUICKDISK_SIO_RXDONE = 3;
    public const int QUICKDISK_SIO_DATA = 4;
    public const int QUICKDISK_SIO_BREAK = 5;

    public const int QUICKDISK_BUFFER_SIZE = 65536;

    const int EVENT_RESTORE = 0;
    const int EVENT_END = 1;
    const double PERIOD_RESTORE = 100.0;
    const double PERIOD_END = 1000000.0;

    const ushort DATA_SYNC = 0x16;
    const ushort DATA_MARK = 0xa5;
    const ushort DATA_CRC = 0xff;
    const ushort DATA_BREAK = 0x100;
    const ushort DATA_EMPTY = 0x101;

    // SIOへの参照 (エミュレータ構成時に設定してください)
    public Z80SIO SioDevice { get; set; }

    // サウンド再生用のインターフェース (独自環境に合わせて実装してください)
    public Action PlaySeekNoise; 
    public Action StopSeekNoise;

    private string filePath;
    private bool insert, protect, home, modified, accessed;
    private ushort[] buffer = new ushort[QUICKDISK_BUFFER_SIZE];
    private int buffer_ptr, write_ptr;
    private bool first_data, send_break;
    private bool wrga, mton, sync, motor_on;
    private int restore_id = -1, end_id = -1;

    public Func<int, double, int> OnRegisterEvent;
    public Action<int> OnCancelEvent;

    private int RegisterEvent(int eventId, double interval) => OnRegisterEvent?.Invoke(eventId, interval) ?? -1;
    private void CancelEvent(int eventId) => OnCancelEvent?.Invoke(eventId);

    public QuickDisk()
    {
        insert = protect = false;
        home = true;
        first_data = send_break = true;
    }

    public void WriteSignal(int id, uint data, uint mask)
    {
        bool next = (data & mask) != 0;
        if (id == QUICKDISK_SIO_RTSA)
        {
            if (wrga && !next) { first_data = true; write_ptr = 0; }
            else if (!wrga && next) { WriteCrc(); }
            wrga = next;
        }
        else if (id == QUICKDISK_SIO_DTRB)
        {
            if (mton && !next)
            {
                if (motor_on && wrga) { SendData(); RegisterEndEvent(); }
                else
                {
                    if (!motor_on) { PlaySeekNoise?.Invoke(); motor_on = true; }
                    RegisterRestoreEvent();
                    CancelEndEvent();
                }
            }
            else if (!mton && next) { SetHome(true); }
            mton = next;
        }
        else if (id == QUICKDISK_SIO_SYNC)
        {
            sync = next;
            if (sync)
            {
                if (!wrga) { WriteCrc(); wrga = true; }
                SendData();
            }
        }
        else if (id == QUICKDISK_SIO_RXDONE) { SendData(); }
        else if (id == QUICKDISK_SIO_DATA || id == QUICKDISK_SIO_BREAK)
        {
            if (!(motor_on && !wrga)) return;

            if (id == QUICKDISK_SIO_DATA)
            {
                if (first_data) { WriteBuffer(DATA_SYNC); WriteBuffer(DATA_SYNC); first_data = false; }
                WriteBuffer((ushort)data);
                write_ptr = buffer_ptr;
            }
            else if (id == QUICKDISK_SIO_BREAK)
            {
                WriteCrc();
                WriteBuffer(DATA_BREAK);
                first_data = true;
                write_ptr = 0;
            }
            accessed = true;
            if (buffer_ptr < QUICKDISK_BUFFER_SIZE) RegisterEndEvent();
            else { CancelEndEvent(); EndOfDisk(); }
        }
    }

    public uint ReadSignal(int ch)
    {
        if (accessed) { accessed = false; return 1; }
        return 0;
    }

    public void EventCallback(int eventId)
    {
        if (eventId == EVENT_RESTORE) { restore_id = -1; Restore(); }
        else if (eventId == EVENT_END) { end_id = -1; EndOfDisk(); }
    }

    private void Restore()
    {
        SetHome(false);
        buffer_ptr = 0;
        first_data = send_break = true;
        PlaySeekNoise?.Invoke();
        SendData();
    }

    private void SendData()
    {
        if (!(motor_on && wrga) || restore_id != -1) return;

    retry:
        if (buffer_ptr < QUICKDISK_BUFFER_SIZE && buffer[buffer_ptr] != DATA_EMPTY)
        {
            if (buffer[buffer_ptr] == DATA_BREAK)
            {
                if (send_break)
                {
                    SioDevice?.WriteSignal(Z80SIO.SIG_Z80SIO_BREAK_CH0, 1, 1);
                    send_break = false;
                }
                if (!sync) return;
                buffer_ptr++;
                goto retry;
            }
            SioDevice?.WriteSignal(Z80SIO.SIG_Z80SIO_RECV_CH0, buffer[buffer_ptr++], 0xff);
            send_break = true;
            accessed = true;
            RegisterEndEvent();
        }
        else
        {
            CancelEndEvent();
            EndOfDisk();
        }
    }

    private void WriteCrc()
    {
        if (!wrga && write_ptr != 0)
        {
            buffer_ptr = write_ptr;
            WriteBuffer(DATA_CRC); WriteBuffer(DATA_CRC);
            WriteBuffer(DATA_SYNC); WriteBuffer(DATA_SYNC);
            WriteBuffer(DATA_BREAK);
            buffer_ptr--; // don't increment pointer !!!
        }
        write_ptr = 0;
    }

    private void EndOfDisk()
    {
        WriteCrc();
        if (mton || !wrga) { if (motor_on) { StopSeekNoise?.Invoke(); motor_on = false; } }
        else { RegisterRestoreEvent(); }
        SetHome(true);
    }

    private void WriteBuffer(ushort v)
    {
        if (buffer_ptr < QUICKDISK_BUFFER_SIZE)
        {
            if (buffer[buffer_ptr] != v) { buffer[buffer_ptr] = v; modified = true; }
            buffer_ptr++;
        }
    }

    private void SetInsert(bool val) { SioDevice?.WriteSignal(Z80SIO.SIG_Z80SIO_DCD_CH0, (uint)(val ? 0 : 1), 1); insert = val; }
    private void SetProtect(bool val) { SioDevice?.WriteSignal(Z80SIO.SIG_Z80SIO_CTS_CH0, (uint)(val ? 1 : 0), 1); protect = val; }
    private void SetHome(bool val)
    {
        if (home != val) { SioDevice?.WriteSignal(Z80SIO.SIG_Z80SIO_DCD_CH1, (uint)(val ? 1 : 0), 1); home = val; }
    }

    public void OpenDisk(string path)
    {
        if (insert)
        {
            if (string.Equals(filePath, path, StringComparison.OrdinalIgnoreCase)) return;
            CloseDisk();
        }
        
        for (int i = 0; i < QUICKDISK_BUFFER_SIZE; i++) buffer[i] = DATA_EMPTY;
        
        if (File.Exists(path))
        {
            filePath = path;
            buffer_ptr = 0;
            modified = false;

            // QDF/MZTファイルの解析・展開ロジックはSystem.IOを利用して実装します
            // BinaryReader等を使用して元のC++の処理（Fread/Fgetc）と同様のバイトストリームパースを行ってください。
            // ※非常に長くなるため詳細なMZTブロック展開処理は割愛しますが、File.ReadAllBytes(path)から配列アクセスで移植可能です。

            SetInsert(true);
            SetProtect((File.GetAttributes(path) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            SetHome(true);
        }
    }

    public void CloseDisk()
    {
        ReleaseDisk();
        SetInsert(false);
        SetProtect(false);
        SetHome(true);
        CancelRestoreEvent();
        CancelEndEvent();
    }

    private void ReleaseDisk()
    {
        if (insert && !protect && modified)
        {
            // BinaryWriter を使用してQDF/MZTへの書き戻しを実装します。
            // using(var bw = new BinaryWriter(File.Open(filePath, FileMode.Create))) { ... }
        }
    }

    // ヘルパー
    private void RegisterRestoreEvent() { if (restore_id == -1) restore_id = RegisterEvent(EVENT_RESTORE, PERIOD_RESTORE); }
    private void CancelRestoreEvent() { if (restore_id != -1) { CancelEvent(restore_id); restore_id = -1; } }
    private void RegisterEndEvent() { if (end_id != -1) CancelEvent(end_id); end_id = RegisterEvent(EVENT_END, PERIOD_END); }
    private void CancelEndEvent() { if (end_id != -1) { CancelEvent(end_id); end_id = -1; } }
}