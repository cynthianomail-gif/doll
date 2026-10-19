# 娃娃工坊（暫名）

一款「從布料親手做娃娃」的接單手作模擬遊戲。客人上門提出需求，玩家自己解讀，從設計、排版、裁布、車縫、塞棉花到裝飾，一步一步做出娃娃，最後交件給客人評價。

參考：[《Dressmaker》](https://store.steampowered.com/app/4019220/Dressmaker/)（做衣服版本的同類遊戲，2026/9 上市，Steam 壓倒性好評）。

## 試玩

用瀏覽器（Chrome 或 Edge）直接打開 [`prototype/index.html`](prototype/index.html)，不需要安裝任何東西。

目前版本：**原型 v0.9**（布偶、搪膠、木頭娃娃；衣服、帽子與配件、衣櫃換裝；盲盒系列與開箱、直播模式；分享代碼；自動存檔）

## 文件

- [設計筆記與決策紀錄](docs/DESIGN.md)：概念、評估、研究、已做的決定、路線圖
- [研究資料](docs/RESEARCH.md)：《Dressmaker》流程拆解、手作娃娃市場調查

## 開發方式

企劃由作者負責，程式與美術全部由 AI（Claude Code）協助製作。原型是單一 HTML 檔，用 Canvas 2D 繪製，沒有外部依賴。
