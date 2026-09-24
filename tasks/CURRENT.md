# CURRENT - v0.1.29

## 目的

ScintillaNETの文字位置とネイティブAPIのUTF-8バイト位置を統一し、URL装飾とクリック判定の行ずれを解消する。

## v0.1.29 スコープ

- [x] 行先頭をネイティブAPIから取得し、UTF-16オフセット変換の基準をUTF-8バイト位置へ統一
- [x] 行単位の装飾解除範囲をネイティブ位置・ネイティブ長で計算
- [x] URLクリック位置をネイティブ位置からUTF-16文字位置へ変換
- [x] 日本語・Markdown・複数行を含む実Scintilla装飾範囲のWindowsスモークテストを追加
- [x] README.md変更履歴・操作説明を更新
- [x] アプリ版をv0.1.29へ更新

## 対象外

- Markdown記法や本文そのものは変更しない
- `ftp://`、`file://`、メールアドレスはリンク表示しない
- URL内で対応している丸括弧は従来どおりURLの一部として扱う

## 次候補

1. operator + motion の完全化（`d%`, `dfx`, `c}`, `y2w`, `3dd` 等）
2. Visualモードとtext object (`iw`, `aw`, `i(`, `a{` 等)
3. mark/jumplist (`m{a-z}`, `'a`, `` `a ``, Ctrl+O/Ctrl+I)
4. wrapped screen-line motion (`gj/gk/g0/g$/g^`)
5. タブの前回セッション復元、ドラッグ並べ替え、ピン留め
6. Markdownライブプレビュー
7. JSONツリー表示 / JSONPath検索
8. UI自動テスト
