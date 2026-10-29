-- ============================================================================
-- V001: 住所データのテーブルを作成する（v0.6）
-- ----------------------------------------------------------------------------
-- DbUp がアプリ起動時に実行する（Flyway と同じ命名規則 V{番号}__{説明}.sql）。
-- 適用済みのスクリプトは書き換えないこと。変更が必要な場合は V004 以降を追加する。
-- 番号は3桁でゼロ埋めする（DbUp はファイル名の文字列順に実行するため、V10 が V2 より先にならないように）。
-- ============================================================================

-- 大阪府の市町村（43件：政令指定都市2・市31・町9・村1）
CREATE TABLE municipalities (
    code          char(5)      PRIMARY KEY,                -- 全国地方公共団体コード（5桁、検査数字なし）
    name          varchar(20)  NOT NULL UNIQUE,            -- 例：大阪市、豊中市、島本町
    roman         varchar(40)  NOT NULL,                   -- 例：Osaka、Toyonaka、Shimamoto
    kind          varchar(10)  NOT NULL
                  CHECK (kind IN ('政令指定都市', '市', '町', '村')),
    county        varchar(20),                             -- 町村の郡名（例：三島郡）。市は NULL
    has_town_data boolean      NOT NULL DEFAULT false,     -- 町名・丁目データを持つか（現在は大阪市のみ）
    sort_order    integer      NOT NULL UNIQUE,            -- Municipalities.json の記載順
    -- 郡名は町村にだけ付く
    CHECK ((kind IN ('町', '村')) = (county IS NOT NULL))
);

-- 政令指定都市の行政区（31件：大阪市24区・堺市7区）
CREATE TABLE wards (
    code              char(5)      PRIMARY KEY,
    municipality_code char(5)      NOT NULL REFERENCES municipalities (code),
    name              varchar(20)  NOT NULL,               -- 例：北区（大阪市と堺市の両方にある）
    roman             varchar(40)  NOT NULL,
    town_source_url   varchar(200),                        -- 町名一覧の出典（大阪市の区のみ）
    sort_order        integer      NOT NULL,
    UNIQUE (municipality_code, name),
    UNIQUE (municipality_code, sort_order)
);

-- 大阪市24区の町名（580件）
CREATE TABLE towns (
    ward_code           char(5)      NOT NULL REFERENCES wards (code),
    name                varchar(30)  NOT NULL,
    allow_without_chome boolean      NOT NULL DEFAULT false, -- 丁目あり・なしの両方を使う町（例：中央区上町）
    sort_order          integer      NOT NULL,
    PRIMARY KEY (ward_code, name),
    UNIQUE (ward_code, sort_order)
);

-- 町ごとの丁目（丁目のない町には行がない）
CREATE TABLE chomes (
    ward_code  char(5)      NOT NULL,
    town_name  varchar(30)  NOT NULL,
    name       varchar(10)  NOT NULL,                      -- 例：1丁目
    sort_order integer      NOT NULL,
    PRIMARY KEY (ward_code, town_name, name),
    UNIQUE (ward_code, town_name, sort_order),
    FOREIGN KEY (ward_code, town_name) REFERENCES towns (ward_code, name) ON DELETE CASCADE
);

COMMENT ON TABLE municipalities IS '大阪府の市町村（43件）';
COMMENT ON TABLE wards          IS '政令指定都市の行政区（大阪市24区・堺市7区）';
COMMENT ON TABLE towns          IS '大阪市24区の町名（出典：大阪市 区別町名一覧）';
COMMENT ON TABLE chomes         IS '町ごとの丁目';
