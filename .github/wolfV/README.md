# wolfV — 狼人吸血鬼中短篇小说创作 Agent 流水线

四个协作 Agent，串行产出一部 1-3 万字的狼人/吸血鬼题材中短篇小说。

## 流水线
```
[01 头脑风暴 & 命名]
        ↓ 大纲 + 唯一书名
[02 大纲市场审核]
        ↓ 通过 / 打回修改
[03 正文写作]（1万-3万字）
        ↓ 正文初稿
[04 名家审稿]
        ↓ 终审报告
```

## Agent 清单
| 序号 | Agent | 文件 | 产物 |
|------|-------|------|------|
| 1 | 头脑风暴 & 命名 | `01-brainstorm-agent.md` | `01-outline.md` |
| 2 | 大纲市场审核 | `02-outline-review-agent.md` | `02-outline-review.md` |
| 3 | 正文写作 | `03-writing-agent.md` | `03-manuscript.md` |
| 4 | 名家审稿 | `04-master-review-agent.md` | `04-final-review.md` |

所有产物输出到 `PrivateStore/novels/<书名>/` 目录下。

## 触发方式
按顺序调用四个 Agent，前一个的产物即下一个的输入。Agent 2 若打回，回到 Agent 1 修改大纲后再次进入流水线。
