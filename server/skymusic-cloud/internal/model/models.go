// 模块：skymusic-cloud 服务模型 models
package model

import "encoding/json"

type LyricLine struct {
	TimeMS int64  `json:"timeMs"`
	Text   string `json:"text"`
}

type Lyrics struct {
	ID       string      `json:"id"`
	Title    string      `json:"title"`
	Artist   string      `json:"artist"`
	Album    string      `json:"album,omitempty"`
	Provider string      `json:"provider"`
	Lines    []LyricLine `json:"lines"`
}

type LyricSummary struct {
	ID     string `json:"id"`
	Title  string `json:"title"`
	Artist string `json:"artist"`
}

type Score struct {
	ID        string          `json:"id"`
	Title     string          `json:"title"`
	Author    string          `json:"author"`
	Format    string          `json:"format"`
	Content   json.RawMessage `json:"content"`
	CreatedAt string          `json:"createdAt"`
}

type Database struct {
	Lyrics []Lyrics `json:"lyrics"`
	Scores []Score  `json:"scores"`
}
