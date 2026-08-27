# MZ-1500 エミュレータ (CommonSourceProjectSrc) ファイル構成
キーワード: `MZ-1500`, `Emulator`, `Source Code`, `C++`, `Takeda`, `EmuZ`, `仕様`
【情報源: CommonSourceProjectSrc/source/src/vm/mz700/】

Takeda.Toshiya 氏作の MZ シリーズエミュレータ・コアにおける、MZ-1500 エミュレーション関連のファイル構成とアーキテクチャの解説です。

## 1. ディレクトリ構造
ベースディレクトリ: `CommonSourceProjectSrc/source/src/vm/`
MZ-1500 の実装は MZ-700 / MZ-800 と共通の基盤を利用しており、主に `mz700` フォルダ内に存在します。コンパイル時のプリプロセッサマクロ (`#if defined(_MZ1500)`) によって処理が分岐します。

## 2. 主要なソースファイル
### 2-A. `mz700` フォルダ内
*   **`mz700.cpp` / `mz700.h`**
    *   メイン初期化処理、および各ペリフェラル間のイベントルーティング（信号の配線）を定義しています。
*   **`memory.cpp` / `memory.h`**
    *   メモリマップと、メモリマップドI/O（E000h〜E00Fh 等）の振り分け処理を実装しています。VRAMウェイト(WAIT)のシミュレーションもここで行われます。
*   **`joystick.cpp`**
    *   V-BLANK信号を利用したPWM変調によるジョイスティック入力のエミュレーション。
*   **`keyboard.cpp`**
    *   8255 を介したキーマトリクス走査。

### 2-B. `vm` フォルダ直下 (共通デバイスモジュール)
*   **`datarec.cpp`**: データレコーダ (CMT) のPWMエンコード/デコード処理。
*   **`quickdisk.cpp`**: クイックディスクのフォーマット処理と Z80 SIO インターフェース。
*   **`psg.cpp` / `sn76489an.cpp`**: テキサスインスツルメンツ系PSG音源。
*   **`ym2151.cpp`**: 拡張FM音源モジュール。
*   **`i8253.cpp` / `i8255.cpp`**: PIT および PPI の汎用エミュレーション。
*   **`z80.cpp` / `z80pio.cpp` / `z80sio.cpp`**: Z80 CPU コアと周辺チップ。
