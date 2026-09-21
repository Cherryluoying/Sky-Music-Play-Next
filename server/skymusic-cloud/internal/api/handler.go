// 模块：skymusic-cloud HTTP 接口 handler
package api

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"strings"
	"time"

	"skymusic-cloud/internal/model"
	"skymusic-cloud/internal/store"
)

const maxBodyBytes = 4 << 20

type Options struct {
	AllowedOrigin string
	WriteToken    string
}

type handler struct {
	repository store.Repository
	options    Options
}

func New(repository store.Repository, options Options) http.Handler {
	h := &handler{repository: repository, options: options}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", h.health)
	mux.HandleFunc("GET /v1/lyrics/search", h.searchLyrics)
	mux.HandleFunc("GET /v1/lyrics/{id}", h.getLyrics)
	mux.HandleFunc("POST /v1/lyrics", h.putLyrics)
	mux.HandleFunc("GET /v1/scores", h.listScores)
	mux.HandleFunc("POST /v1/scores", h.putScore)
	return h.middleware(mux)
}

func (h *handler) health(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]any{
		"status":  "ok",
		"service": "skymusic-cloud",
		"time":    time.Now().UTC().Format(time.RFC3339),
	})
}

// searchLyrics 按标题和作者检索歌词摘要
func (h *handler) searchLyrics(w http.ResponseWriter, r *http.Request) {
	items, err := h.repository.SearchLyrics(r.Context(), r.URL.Query().Get("title"), r.URL.Query().Get("artist"))
	if err != nil {
		writeError(w, http.StatusInternalServerError, "search_failed", err)
		return
	}
	writeJSON(w, http.StatusOK, items)
}

func (h *handler) getLyrics(w http.ResponseWriter, r *http.Request) {
	item, found, err := h.repository.GetLyrics(r.Context(), r.PathValue("id"))
	if err != nil {
		writeError(w, http.StatusInternalServerError, "read_failed", err)
		return
	}
	if !found {
		writeError(w, http.StatusNotFound, "not_found", errors.New("lyrics not found"))
		return
	}
	writeJSON(w, http.StatusOK, item)
}

// putLyrics 校验写入令牌并保存歌词
func (h *handler) putLyrics(w http.ResponseWriter, r *http.Request) {
	if !h.authorized(r) {
		writeError(w, http.StatusUnauthorized, "unauthorized", errors.New("invalid write token"))
		return
	}
	var item model.Lyrics
	if err := decodeJSON(w, r, &item); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_json", err)
		return
	}
	if strings.TrimSpace(item.Title) == "" || len(item.Lines) == 0 {
		writeError(w, http.StatusBadRequest, "invalid_lyrics", errors.New("title and lines are required"))
		return
	}
	if item.ID == "" {
		item.ID = newID()
	}
	if item.Provider == "" {
		item.Provider = "SkyMusic Cloud"
	}
	if err := h.repository.PutLyrics(r.Context(), item); err != nil {
		writeError(w, http.StatusInternalServerError, "write_failed", err)
		return
	}
	writeJSON(w, http.StatusCreated, item)
}

func (h *handler) listScores(w http.ResponseWriter, r *http.Request) {
	items, err := h.repository.ListScores(r.Context())
	if err != nil {
		writeError(w, http.StatusInternalServerError, "read_failed", err)
		return
	}
	writeJSON(w, http.StatusOK, items)
}

// putScore 校验写入令牌并保存乐谱
func (h *handler) putScore(w http.ResponseWriter, r *http.Request) {
	if !h.authorized(r) {
		writeError(w, http.StatusUnauthorized, "unauthorized", errors.New("invalid write token"))
		return
	}
	var item model.Score
	if err := decodeJSON(w, r, &item); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_json", err)
		return
	}
	if strings.TrimSpace(item.Title) == "" || len(item.Content) == 0 || !json.Valid(item.Content) {
		writeError(w, http.StatusBadRequest, "invalid_score", errors.New("title and valid JSON content are required"))
		return
	}
	if item.ID == "" {
		item.ID = newID()
	}
	if item.Format == "" {
		item.Format = "sky-studio"
	}
	if item.CreatedAt == "" {
		item.CreatedAt = time.Now().UTC().Format(time.RFC3339)
	}
	if err := h.repository.PutScore(r.Context(), item); err != nil {
		writeError(w, http.StatusInternalServerError, "write_failed", err)
		return
	}
	writeJSON(w, http.StatusCreated, item)
}

// middleware 统一处理跨域和预检请求
func (h *handler) middleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		origin := h.options.AllowedOrigin
		if origin != "" {
			w.Header().Set("Access-Control-Allow-Origin", origin)
			w.Header().Set("Vary", "Origin")
		}
		w.Header().Set("Access-Control-Allow-Headers", "Authorization, Content-Type")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
		w.Header().Set("X-Content-Type-Options", "nosniff")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}

func (h *handler) authorized(r *http.Request) bool {
	return h.options.WriteToken == "" || r.Header.Get("Authorization") == "Bearer "+h.options.WriteToken
}

func decodeJSON(w http.ResponseWriter, r *http.Request, target any) error {
	r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)
	decoder := json.NewDecoder(r.Body)
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(target); err != nil {
		return err
	}
	if err := decoder.Decode(&struct{}{}); !errors.Is(err, io.EOF) {
		return errors.New("request body must contain one JSON value")
	}
	return nil
}

func writeJSON(w http.ResponseWriter, status int, value any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(value)
}

func writeError(w http.ResponseWriter, status int, code string, err error) {
	writeJSON(w, status, map[string]string{"code": code, "message": err.Error()})
}

func newID() string {
	var bytes [16]byte
	if _, err := rand.Read(bytes[:]); err != nil {
		return time.Now().UTC().Format("20060102150405.000000000")
	}
	return hex.EncodeToString(bytes[:])
}
