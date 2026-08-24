# セッションログ

## 今回の作業内容 (画面描画の修正 & PCG描画 & QDF読み込み対応)
- **VRAMアトリビュートの前景色/背景色デコード修正**:
  - MZ-700/1500 のアトリビュート仕様（上位ニブル: 前景色、下位ニブル: 背景色）に合わせて `EmulatorWindow.axaml.cs` のデコードロジックを修正。
- **Avalonia UI 描画更新 (`InvalidateVisual`) の追加**:
  - `WriteableBitmap` へのフレームバッファ書き込み後に `Image.InvalidateVisual()` を呼び出し、毎フレーム確実に画面が更新されるように修正。
- **MZ-1500 PCG (カラーキャラクタジェネレータ) 描画の実装**:
  - `priority` (Port 0xF0) および `pcg_attr` (Port 0xDC00) に基づく PCG と テキストの重ね合わせ描画（8色合成）を完全実装。
- **Quick Disk (.qdf) ファイルのロード対応**:
  - `Mz1500Machine.LoadQdf(path)` を実装し、ヘッダブロック/データブロックからバイナリおよびPCGデータをRAMに展開し、実行アドレスへジャンプする処理を追加。
  - `MainWindow.axaml.cs` で `.qdf` 選択時に `LoadQdf` を呼び出すように接続。

## 未解決の課題 / 残件 (次回への引き継ぎ)

### 【残件】サウンド出力の統合
- PSG (SN76489) および YM2151 のI/Oポート書き込みデータを Avalonia/NAudio などのオーディオ出力パイプラインへ接続する。

### 【残件】キーボード入力のGUIイベント連携
- `EmulatorWindow` のキーボード入力イベントを `Keyboard` クラスのマトリクスに反映する。
