# Boxora. Everything routes through start.sh so there is one source of truth
# for config, database bootstrap and migrations.
.DEFAULT_GOAL := help
.PHONY: help up run db migrate seed reseed embed smoke build reset clean psql corpus

help: ## Show this help
	@grep -hE '^[a-z-]+:.*?## ' $(MAKEFILE_LIST) | awk -F':.*?## ' '{printf "  \033[38;5;173m%-10s\033[0m %s\n", $$1, $$2}'

up: ## Database + migrations + build + seed if empty + run (the one command)
	@./start.sh

run: up ## Alias for up

db: ## Create the database/role in the Docker Postgres and apply migrations
	@./start.sh --migrate-only

migrate: db ## Alias for db

seed: ## Load the twelve demo claims and run the full pipeline over them
	@./start.sh --seed

reseed: ## Wipe the database, migrate, re-seed from scratch
	@./start.sh --reset --migrate-only && ./start.sh --seed

embed: ## Embed new or changed legal passages into pgvector
	@./start.sh --embed

smoke: ## Offline check suite — no Azure credentials needed
	@./start.sh --smoke

build: ## Compile only
	@dotnet build src/JbAutoAi -v q --nologo

reset: ## Drop and recreate the database, then apply migrations
	@./start.sh --reset --migrate-only

clean: ## Remove build output and uploaded files
	@rm -rf src/JbAutoAi/bin src/JbAutoAi/obj
	@find uploads -mindepth 1 -maxdepth 1 -type d -exec rm -rf {} +
	@echo "cleaned"

psql: ## Open a psql shell on the application database
	@set -a; . ./.env; set +a; \
	docker exec -it $${PG_CONTAINER:-counted-db-1} psql -U $${PG_USER:-jbauto} -d $${PG_DB:-jb_auto_ai}

corpus: ## Show the legal corpus status
	@set -a; . ./.env; set +a; \
	docker exec -i $${PG_CONTAINER:-counted-db-1} psql -U $${PG_USER:-jbauto} -d $${PG_DB:-jb_auto_ai} -c \
	"SELECT v.id AS corpus, d.doc_class, count(*) AS chunks, count(c.embedding) AS embedded \
	 FROM legal_chunk c JOIN legal_doc d ON d.id = c.doc_id \
	 JOIN legal_corpus_version v ON v.id = d.corpus_version \
	 GROUP BY 1, 2 ORDER BY 2;"
