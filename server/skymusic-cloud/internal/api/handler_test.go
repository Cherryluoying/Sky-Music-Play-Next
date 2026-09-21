// 模块：skymusic-cloud HTTP 接口 handler_test
package api

import (
	"bytes"
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"skymusic-cloud/internal/model"
)

type memoryStore struct {
	lyrics []model.Lyrics
	scores []model.Score
}

func (s *memoryStore) SearchLyrics(_ context.Context, _, _ string) ([]model.LyricSummary, error) {
	result := make([]model.LyricSummary, 0, len(s.lyrics))
	for _, item := range s.lyrics {
		result = append(result, model.LyricSummary{ID: item.ID, Title: item.Title, Artist: item.Artist})
	}
	return result, nil
}
func (s *memoryStore) GetLyrics(_ context.Context, id string) (model.Lyrics, bool, error) {
	for _, item := range s.lyrics {
		if item.ID == id {
			return item, true, nil
		}
	}
	return model.Lyrics{}, false, nil
}
func (s *memoryStore) PutLyrics(_ context.Context, item model.Lyrics) error {
	s.lyrics = append(s.lyrics, item)
	return nil
}
func (s *memoryStore) ListScores(_ context.Context) ([]model.Score, error) { return s.scores, nil }
func (s *memoryStore) PutScore(_ context.Context, item model.Score) error {
	s.scores = append(s.scores, item)
	return nil
}

func TestHealth(t *testing.T) {
	request := httptest.NewRequest(http.MethodGet, "/health", nil)
	response := httptest.NewRecorder()
	New(&memoryStore{}, Options{}).ServeHTTP(response, request)
	if response.Code != http.StatusOK {
		t.Fatalf("status = %d", response.Code)
	}
}

func TestPutAndGetLyrics(t *testing.T) {
	store := &memoryStore{}
	handler := New(store, Options{WriteToken: "secret"})
	payload, _ := json.Marshal(model.Lyrics{Title: "Song", Lines: []model.LyricLine{{Text: "line"}}})
	request := httptest.NewRequest(http.MethodPost, "/v1/lyrics", bytes.NewReader(payload))
	request.Header.Set("Authorization", "Bearer secret")
	response := httptest.NewRecorder()
	handler.ServeHTTP(response, request)
	if response.Code != http.StatusCreated || len(store.lyrics) != 1 {
		t.Fatalf("status = %d, lyrics = %d", response.Code, len(store.lyrics))
	}

	request = httptest.NewRequest(http.MethodGet, "/v1/lyrics/"+store.lyrics[0].ID, nil)
	response = httptest.NewRecorder()
	handler.ServeHTTP(response, request)
	if response.Code != http.StatusOK {
		t.Fatalf("status = %d", response.Code)
	}
}
