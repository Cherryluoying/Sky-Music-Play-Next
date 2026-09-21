// 模块：skymusic-cloud 歌词与乐谱服务入口
package main

import (
	"log"
	"net/http"
	"os"
	"time"

	"skymusic-cloud/internal/api"
	"skymusic-cloud/internal/store"
)

func main() {
	address := env("SKYMUSIC_ADDRESS", ":8787")
	dataFile := env("SKYMUSIC_DATA_FILE", "data/store.json")

	repository, err := store.NewFileStore(dataFile)
	if err != nil {
		log.Fatalf("open data store: %v", err)
	}

	handler := api.New(repository, api.Options{
		AllowedOrigin: env("SKYMUSIC_CORS_ORIGIN", "http://localhost"),
		WriteToken:    os.Getenv("SKYMUSIC_WRITE_TOKEN"),
	})
	server := &http.Server{
		Addr:              address,
		Handler:           handler,
		ReadHeaderTimeout: 5 * time.Second,
		ReadTimeout:       15 * time.Second,
		WriteTimeout:      30 * time.Second,
		IdleTimeout:       60 * time.Second,
	}

	log.Printf("SkyMusic Cloud listening on %s", address)
	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}

func env(name, fallback string) string {
	if value := os.Getenv(name); value != "" {
		return value
	}
	return fallback
}
