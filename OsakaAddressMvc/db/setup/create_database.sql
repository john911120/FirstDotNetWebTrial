-- ============================================================================
-- 開発用 DB の作成（Docker を使わない場合のみ・初回に手動で実行）
-- ----------------------------------------------------------------------------
-- 通常はソリューションのルートの compose.yaml を使う（docker compose up -d）。
-- その場合、ロール oamvc_user と DB oamvc はコンテナーの初回起動時に作成されるため、このファイルは不要。
--
-- このファイルは、既存の PostgreSQL に OsakaAddressMvc 専用のロールと DB を作成する場合に使う。
-- テーブルの作成とデータ投入は、アプリ起動時に DbUp が db/migration/ のスクリプトで行う。
--
-- 実行例（管理者ユーザーで接続し、このファイルを実行する。ポートは既存の PostgreSQL のもの）：
--   psql -h localhost -p 5432 -U postgres -f db/setup/create_database.sql
--
-- ※ パスワードは実行前に必ず書き換えること（このファイルに本物のパスワードを保存してコミットしないこと）。
-- ※ CREATE DATABASE はトランザクション内で実行できないため、psql の -1 オプションは付けないこと。
-- ※ PostgreSQL は引用符なしの名前を小文字に変換するため、DB 名・ロール名は小文字で統一している。
-- ============================================================================

CREATE ROLE oamvc_user LOGIN PASSWORD 'change-me';

CREATE DATABASE oamvc OWNER oamvc_user ENCODING 'UTF8';
