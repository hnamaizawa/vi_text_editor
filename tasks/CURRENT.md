# CURRENT - v0.1.0

## 目的

EmEditor／サクラエディタに近いWindows GUIを持ち、編集キーバインドだけをvi方式にしたテキストエディターの初期版を成立させる。

## v0.1.0 スコープ

- [x] .NET 10 + WinFormsのプロジェクト構成
- [x] Scintilla5.NETの導入
- [x] viキープロセッサをGUIから分離
- [x] NORMAL / INSERT
- [x] `h j k l`
- [x] `w b e`
- [x] `0 ^ $`
- [x] `gg G`
- [x] `x dd yy p P`
- [x] `u Ctrl+R`
- [x] `i a o O`
- [x] UTF-8 / UTF-16 / Shift_JIS読み込み・保存
- [x] 未保存変更確認
- [x] Core自動テスト
- [x] GitHub Actions

## 次候補

1. 検索 (`/`, `n`, `N`) とWindows検索ダイアログ
2. タブ編集
3. Visualモード
4. 数値プレフィックス
5. operator + motion
