-- ============================================================================
-- V004: 都道府県のテーブルを追加する（v0.6.0）
-- ----------------------------------------------------------------------------
-- DB から住所データを読み込む（DatabaseAddressDataSource）ために、
-- JSON の先頭にあった都道府県の情報（名前・コード・市町村一覧の出典）を DB にも持たせる。
-- 適用済みの V001 は書き換えず、新しい番号のスクリプトとして追加する（マイグレーションの原則）。
-- ============================================================================

CREATE TABLE prefectures (
    code       char(2)       PRIMARY KEY,              -- 都道府県コード（大阪府 = 27）
    name       varchar(10)   NOT NULL UNIQUE,
    source_url varchar(200)  NOT NULL                  -- 市町村一覧の出典
);

COMMENT ON TABLE prefectures IS '都道府県（市町村一覧の出典を含む）';

INSERT INTO prefectures (code, name, source_url) VALUES
  ('27', '大阪府', 'https://www.pref.osaka.lg.jp/o040050/shichoson/juki/juki2012.html');
