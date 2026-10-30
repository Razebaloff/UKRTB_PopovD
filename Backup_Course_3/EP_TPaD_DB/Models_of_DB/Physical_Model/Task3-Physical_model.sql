-- LPPopov: запись на дополнительные тренировки по хоккею.
-- Источник: логическая модель и концептуальная PNG, commit f1821d2.
-- В pgAdmin выберите базу HockeyCentre, откройте Query Tool и выполните файл целиком.
-- Запускать один раз, до создания одноимённых таблиц в схеме public.
-- Линейные размеры указаны в метрах, масса в кг, цены в рублях.

BEGIN;
SET LOCAL search_path = public;

CREATE TABLE human (
    id_human INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    last_name VARCHAR(100) NOT NULL CHECK (btrim(last_name) <> ''),
    name VARCHAR(100) NOT NULL CHECK (btrim(name) <> ''),
    middle_name VARCHAR(100)
);

CREATE TABLE size (
    id_size INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    length NUMERIC(10,3) NOT NULL CHECK (length > 0),
    width NUMERIC(10,3) NOT NULL CHECK (width > 0),
    height NUMERIC(10,3) NOT NULL CHECK (height > 0)
);

CREATE TABLE hockey_gates (
    id_hockey_gates INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    depth NUMERIC(10,3) NOT NULL CHECK (depth > 0),
    length NUMERIC(10,3) NOT NULL CHECK (length > 0),
    width NUMERIC(10,3) NOT NULL CHECK (width > 0),
    height NUMERIC(10,3) NOT NULL CHECK (height > 0),
    grid BOOLEAN NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity >= 0)
);

CREATE TABLE cones_pack (
    id_cones_pack INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    quantity INTEGER NOT NULL CHECK (quantity >= 0),
    length NUMERIC(10,3) NOT NULL CHECK (length > 0),
    width NUMERIC(10,3) NOT NULL CHECK (width > 0),
    height NUMERIC(10,3) NOT NULL CHECK (height > 0)
);

CREATE TABLE pack_washers (
    id_pack_washers INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    quantity INTEGER NOT NULL CHECK (quantity >= 0),
    length NUMERIC(10,3) NOT NULL CHECK (length > 0),
    width NUMERIC(10,3) NOT NULL CHECK (width > 0),
    weight_kg NUMERIC(10,3) NOT NULL CHECK (weight_kg > 0),
    height NUMERIC(10,3) NOT NULL CHECK (height > 0),
    type VARCHAR(50) NOT NULL CHECK (btrim(type) <> '')
);

CREATE TABLE sports_equipment (
    id_sports_equipment INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_hockey_gates INTEGER NOT NULL UNIQUE
        REFERENCES hockey_gates (id_hockey_gates) ON DELETE RESTRICT,
    id_cones_pack INTEGER NOT NULL UNIQUE
        REFERENCES cones_pack (id_cones_pack) ON DELETE RESTRICT,
    id_pack_washers INTEGER NOT NULL UNIQUE
        REFERENCES pack_washers (id_pack_washers) ON DELETE RESTRICT
);

CREATE TABLE training_zone (
    id_training_zone INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_size INTEGER NOT NULL
        REFERENCES size (id_size) ON DELETE RESTRICT,
    id_sports_equipment INTEGER NOT NULL
        REFERENCES sports_equipment (id_sports_equipment) ON DELETE RESTRICT,
    number INTEGER NOT NULL CHECK (number > 0)
);

CREATE TABLE service (
    id_service INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_training_zone INTEGER NOT NULL
        REFERENCES training_zone (id_training_zone) ON DELETE RESTRICT
);

CREATE TABLE rent_session (
    id_rent_session INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_service INTEGER NOT NULL
        REFERENCES service (id_service) ON DELETE RESTRICT,
    price NUMERIC(12,2) NOT NULL CHECK (price >= 0),
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    duration INTERVAL GENERATED ALWAYS AS (end_time - start_time) STORED,
    CHECK (end_time > start_time)
);

CREATE TABLE accept (
    id_accept INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    condition VARCHAR(30) NOT NULL CHECK (btrim(condition) <> '')
);

CREATE TABLE reserve_on_order (
    id_reserve_on_order INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    price NUMERIC(12,2) NOT NULL CHECK (price >= 0)
);

CREATE TABLE order_on_rent (
    id_order_on_rent INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_human INTEGER NOT NULL
        REFERENCES human (id_human) ON DELETE RESTRICT,
    id_accept INTEGER NOT NULL UNIQUE
        REFERENCES accept (id_accept) ON DELETE RESTRICT,
    id_reserve_on_order INTEGER NOT NULL UNIQUE
        REFERENCES reserve_on_order (id_reserve_on_order) ON DELETE RESTRICT,
    id_rent_session INTEGER NOT NULL
        REFERENCES rent_session (id_rent_session) ON DELETE RESTRICT
);

COMMIT;

-- После выполнения в Data Output появятся 12 созданных таблиц и числа их ключей.
SELECT
    t.table_name,
    (SELECT COUNT(*)
     FROM information_schema.table_constraints AS c
     WHERE c.table_schema = t.table_schema
       AND c.table_name = t.table_name
       AND c.constraint_type = 'PRIMARY KEY') AS primary_keys,
    (SELECT COUNT(*)
     FROM information_schema.table_constraints AS c
     WHERE c.table_schema = t.table_schema
       AND c.table_name = t.table_name
       AND c.constraint_type = 'FOREIGN KEY') AS foreign_keys
FROM information_schema.tables AS t
WHERE t.table_schema = 'public'
  AND t.table_type = 'BASE TABLE'
  AND t.table_name IN (
      'human', 'size', 'hockey_gates', 'cones_pack', 'pack_washers',
      'sports_equipment', 'training_zone', 'service', 'rent_session',
      'accept', 'reserve_on_order', 'order_on_rent'
  )
ORDER BY t.table_name;