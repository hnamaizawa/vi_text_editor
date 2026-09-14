# CURRENT - v0.1.4

## 目的

EmEditor／サクラエディタに近いWindows GUIを維持しつつ、カーソル視認性、日本語等幅表示、viナビゲーションを改善する。

## v0.1.4 スコープ

- [x] NORMALモードのカーソルをブロック表示にする
- [x] INSERTモードのカーソルを3px幅の縦線にする
- [x] 日本語等幅フォントとして `BIZ UDGothic` を優先する
- [x] `BIZ UDGothic` が無い場合は `MS Gothic`、次に `Consolas` へフォールバック
- [x] `w` / `b` をVimのword境界に近づける
- [x] `W` / `B` を空白区切りのWORD移動として追加
- [x] `Ctrl+F` / `Ctrl+B` でほぼ1画面分の下／上スクロールを追加
- [x] word / WORD / ページ移動のCore単体テストを追加
- [x] 参照モード、Undo下限保護、ファイルI/Oを維持

## 次候補

1. `e` / `E`, `ge` / `gE` のword/WORD末尾移動
2. 検索 (`/`, `?`, `n`, `N`) とWindows検索ダイアログ
3. 数値プレフィックス (`3w`, `5j`, `2Ctrl+F` など)
4. operator + motion (`dw`, `dW`, `cw`, `c$` など)
5. Visualモード
6. タブ編集
