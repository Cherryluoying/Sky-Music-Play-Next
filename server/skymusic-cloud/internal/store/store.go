// 模块：skymusic-cloud 数据存储 store
package store

import (
	"context"

	"skymusic-cloud/internal/model"
)

type Repository interface {
	SearchLyrics(ctx context.Context, title, artist string) ([]model.LyricSummary, error)
	GetLyrics(ctx context.Context, id string) (model.Lyrics, bool, error)
	PutLyrics(ctx context.Context, lyrics model.Lyrics) error
	ListScores(ctx context.Context) ([]model.Score, error)
	PutScore(ctx context.Context, score model.Score) error
}
