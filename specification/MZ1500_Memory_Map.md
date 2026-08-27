# MZ-1500 メモリマップ & ROM配置
キーワード: `MZ-1500`, `Memory Map`, `ROM`, `IPL.ROM`, `FONT.ROM`, `EXT.ROM`, `Emulator`, `仕様`
【情報源: CommonSourceProjectSrc/source/src/vm/mz700/memory.cpp】

## 1. 標準メモリマップ (通常時)
基本となるRAMは64KBで構成されます。特定のポート操作で一部がI/OやROMに切り替わります。
*   `0000h` 〜 `CFFFh`: メインRAM (52KB)
*   `D000h` 〜 `DFFFh`: VRAM / PCG (バンク切り替えにより変化。デフォルトはVRAM)
*   `E000h` 〜 `EFFFh`: メモリマップド I/O (8255, 8253 等) および 拡張RAM領域
*   `F000h` 〜 `FFFFh`: システムRAM・ワークエリア

## 2. 必須ROMイメージのファイル名とマッピング
エミュレータ(`memory.cpp`)起動時に読み込まれる実機ROMイメージのファイル名と、メモリへの初期配置です。
*   **`IPL.ROM`**:
    *   IPL (Initial Program Loader) 等のシステムROM。起動時やモニタモード時に `0000h` 等にマッピングされます。
*   **`FONT.ROM`**:
    *   文字フォントを描画するための CGROM (Character Generator ROM)。PCG未使用時のテキスト表示においてフォントパターンとして参照されます。
*   **`EXT.ROM`**:
    *   拡張ROM(辞書ROM等)。ファイルサイズが 6KB (0x1800) の場合は `E800h` へ、8KB (0x2000) の場合は `E000h` へロードされます。
