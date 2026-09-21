// 模块：skymusic-cloud 数据存储 file_store
package store

import (
	"context"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"strings"
	"sync"

	"skymusic-cloud/internal/model"
)

type FileStore struct {
	mu       sync.RWMutex
	path     string
	database model.Database
}

func NewFileStore(path string) (*FileStore, error) {
	store := &FileStore{path: path}
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return store, nil
	}
	if err != nil {
		return nil, err
	}
	if len(data) > 0 {
		if err := json.Unmarshal(data, &store.database); err != nil {
			return nil, err
		}
	}
	return store, nil
}

// SearchLyrics 在内存快照中执行不区分大小写的筛选
func (s *FileStore) SearchLyrics(_ context.Context, title, artist string) ([]model.LyricSummary, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()

	title = normalize(title)
	artist = normalize(artist)
	result := make([]model.LyricSummary, 0)
	for _, item := range s.database.Lyrics {
		if title != "" && !strings.Contains(normalize(item.Title), title) {
			continue
		}
		if artist != "" && !strings.Contains(normalize(item.Artist), artist) {
			continue
		}
		result = append(result, model.LyricSummary{ID: item.ID, Title: item.Title, Artist: item.Artist})
		if len(result) == 20 {
			break
		}
	}
	return result, nil
}

func (s *FileStore) GetLyrics(_ context.Context, id string) (model.Lyrics, bool, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	for _, item := range s.database.Lyrics {
		if item.ID == id {
			return item, true, nil
		}
	}
	return model.Lyrics{}, false, nil
}

// PutLyrics 加锁更新歌词并立即持久化
func (s *FileStore) PutLyrics(_ context.Context, lyrics model.Lyrics) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for index := range s.database.Lyrics {
		if s.database.Lyrics[index].ID == lyrics.ID {
			s.database.Lyrics[index] = lyrics
			return s.persistLocked()
		}
	}
	s.database.Lyrics = append(s.database.Lyrics, lyrics)
	return s.persistLocked()
}

func (s *FileStore) ListScores(_ context.Context) ([]model.Score, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	result := make([]model.Score, len(s.database.Scores))
	copy(result, s.database.Scores)
	return result, nil
}

func (s *FileStore) PutScore(_ context.Context, score model.Score) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	for index := range s.database.Scores {
		if s.database.Scores[index].ID == score.ID {
			s.database.Scores[index] = score
			return s.persistLocked()
		}
	}
	s.database.Scores = append(s.database.Scores, score)
	return s.persistLocked()
}

// persistLocked 通过临时文件原子替换数据文件
func (s *FileStore) persistLocked() error {
	data, err := json.MarshalIndent(s.database, "", "  ")
	if err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(s.path), 0o755); err != nil {
		return err
	}
	temporary := s.path + ".tmp"
	if err := os.WriteFile(temporary, data, 0o600); err != nil {
		return err
	}
	// 同卷重命名避免服务中断留下半份数据
	return os.Rename(temporary, s.path)
}

func normalize(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
