# MZ-1500 クイックディスク (QuickDisk / QD) 仕様
キーワード: `MZ-1500`, `QuickDisk`, `QD`, `Z80SIO`, `SIO`, `Storage`, `Media`, `Emulator`, `仕様`
【情報源: CommonSourceProjectSrc/source/src/vm/mz700/quickdisk.cpp, 紅茶羊羹氏サイト】

MZ-1500 の主力外部ストレージであるクイックディスク (QD) は、Z80 SIO を使用してシリアルデータ通信を行います。

## 1. ハードウェア I/O (Z80 SIO)
*   **`F4h` (R/W)**: SIO Ch.A データ
*   **`F5h` (R/W)**: SIO Ch.B データ
*   **`F6h` (R/W)**: SIO Ch.A コントロール
*   **`F7h` (R/W)**: SIO Ch.B コントロール
*   **信号マッピング**:
    *   DCDA = メディアスイッチ
    *   CTSA = ライトプロテクト
    *   RTSA = WRITE GATE
    *   DCDB = HOME (ヘッド位置検出)
    *   DTRB = モータON
    *   RTSB = VFOイネーブル
*   **通信クロック**:
    *   エミュレータ上のボーレート設定は送受信ともに **`101,562.5 Hz (約 101.5 kHz)`** です。

## 2. フォーマットとデータ構造 (ディスクイメージ)
クイックディスクは1本のトラックがスパイラル状に記録されるシーケンシャルアクセス媒体であり、物理的な記録方式として **MFM (Modified Frequency Modulation)** が採用されています。これにより、磁気反転密度はFM方式の半分で済み、2倍の記録密度が得られます。

*   **物理構造**: 
    メディアトップ -> (160ms禁止領域) -> BLOCK-FILE -> DATA-FILE 1..n -> END
*   **BLOCK FILE (ディレクトリ等)**: 
    SEND BREAK(220ms) -> SYNC(0.7ms) -> DATA MARK(01H) -> BLOCKS(1Byte) -> CRC#1,2 -> SYNC
*   **DATA FILE (プログラムやデータ)**: 
    SEND BREAK(初回220ms / 2回目以降50ms) -> SYNC -> DATA MARK(01H) -> FILE DATA -> CRC#1,2 -> SYNC
    *   **FILE DATA**: BLK FLG (1Byte) + BLK SIZE (2Byte) + BLK DATA (最大64KByte)
    *   **インフォメーションブロック (ヘッダ)**: 属性(1), ネーム(16), LOCK(1), Secret(1), SIZE(2), LOAD ADDR(2), EXEC ADDR(2), COMENT(38) で構成される64バイトのデータです。

## 3. ソフトウェア・インターフェース (BIOS)
BIOS 経由でのアクセス (QDTBL, `$FA00`)
*   `$1130` (QDPA): コマンド (01=Ready check, 02=Format, 03=Read, 04=Write, 05=Header point clear, 06=Motor off)
*   `$1131` (QDPB): パラメータフラグ
*   `$1132` (QDPC): DATA先頭アドレス1
*   `$1134` (QDPE): DATAバイト数1
*   エラーコード (Acc): 40(ファイルなし), 41(ハードエラー), 46(ライトプロテクト), 53(容量不足), 54(未フォーマット)
