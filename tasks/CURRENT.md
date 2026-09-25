# CURRENT - v0.1.31

## 目的

Markdown URLだけを正確に強調し、初期無題タブの自動整理とタブのドラッグ並べ替えを追加する。

## v0.1.31 スコープ

- [x] Markdownリンク全体のlexer色を通常文字色へ変更
- [x] `http://` / `https://` の正確な範囲だけをURLインジケーターで強調
- [x] 唯一の未編集・空の無題タブをファイルオープン成功後に削除
- [x] 編集済み・文字入力済み無題タブを保護
- [x] タブ見出しのマウスドラッグ並べ替えを追加
- [x] Portable／Single-file Windowsスモークテストを追加
- [x] README.md変更履歴・操作説明を更新
- [x] ハーネスとアプリ版をv0.1.31へ更新

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
