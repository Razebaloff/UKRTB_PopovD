-- Задание 6. Учебные данные для хоккейного центра: ровно 100 строк в каждой таблице.
-- Всего: 12 таблиц и 1200 строк. Все имена, цены и характеристики вымышлены.
-- Сначала выполните Physical_Model_Draft.sql. Повторно создавать таблицы не нужно.
-- Затем в Query Tool базы HockeyCentre выполните ЭТОТ файл целиком.
-- Таблицы должны быть пустыми. Если данные уже есть, скрипт остановится.
-- При ошибке выполните ROLLBACK; перед следующим запуском.

BEGIN;
SET LOCAL search_path = public;

-- Исключаем изменение таблиц другими подключениями во время заполнения.
LOCK TABLE human, size, hockey_gates, cones_pack, pack_washers,
    sports_equipment, training_zone, service, rent_session,
    accept, reserve_on_order, order_on_rent
    IN SHARE ROW EXCLUSIVE MODE;

DO $$
DECLARE
    i INTEGER;
    v_table TEXT;
    v_count BIGINT;
    v_tables TEXT[] := ARRAY[
        'human', 'size', 'hockey_gates', 'cones_pack', 'pack_washers',
        'sports_equipment', 'training_zone', 'service', 'rent_session',
        'accept', 'reserve_on_order', 'order_on_rent'
    ];

    v_last_names TEXT[] := ARRAY[
        'Иванов', 'Петров', 'Сидоров', 'Смирнов', 'Кузнецов',
        'Попов', 'Васильев', 'Соколов', 'Михайлов', 'Новиков',
        'Фёдоров', 'Морозов', 'Волков', 'Алексеев', 'Лебедев',
        'Семёнов', 'Егоров', 'Павлов', 'Козлов', 'Степанов'
    ];
    v_names TEXT[] := ARRAY['Александр', 'Дмитрий', 'Максим', 'Иван', 'Артём'];
    v_middle_names TEXT[] := ARRAY[
        'Александрович', 'Дмитриевич', 'Сергеевич', 'Андреевич', 'Иванович',
        'Михайлович', 'Алексеевич', 'Павлович', 'Владимирович', 'Николаевич'
    ];
    -- Это примеры состояний, а не новый перечень ограничений в структуре БД.
    v_conditions TEXT[] := ARRAY['Новый', 'Подтверждён', 'Отменён'];

    v_human_id INTEGER;
    v_size_id INTEGER;
    v_gates_id INTEGER;
    v_cones_id INTEGER;
    v_washers_id INTEGER;
    v_equipment_id INTEGER;
    v_zone_id INTEGER;
    v_service_id INTEGER;
    v_session_id INTEGER;
    v_accept_id INTEGER;
    v_reserve_id INTEGER;

    -- Сохраняем фактически выданные ID: они не обязаны начинаться с единицы.
    v_human_ids INTEGER[] := ARRAY[]::INTEGER[];
    v_session_ids INTEGER[] := ARRAY[]::INTEGER[];
    v_session_prices NUMERIC[] := ARRAY[]::NUMERIC[];
    v_session_index INTEGER;
    v_start TIMESTAMP;
    v_minutes INTEGER;
    v_price NUMERIC(12,2);
BEGIN
    -- Проверяем пустоту всех таблиц до первой вставки.
    FOREACH v_table IN ARRAY v_tables LOOP
        EXECUTE format('SELECT COUNT(*) FROM public.%I', v_table) INTO v_count;
        IF v_count <> 0 THEN
            RAISE EXCEPTION
                'Таблица public.% уже содержит % строк. Нужны пустые таблицы.',
                v_table, v_count;
        END IF;
    END LOOP;

    -- Сначала создаём людей, инвентарь, зоны, услуги и сеансы.
    FOR i IN 1..100 LOOP
        INSERT INTO human (last_name, name, middle_name)
        VALUES (
            v_last_names[1 + (i - 1) % 20],
            v_names[1 + (i - 1) / 20],
            v_middle_names[1 + (i - 1) % 10]
        )
        RETURNING id_human INTO v_human_id;
        v_human_ids[i] := v_human_id;

        -- Размеры в метрах. Повторение характеристик у разных записей допустимо.
        INSERT INTO size (length, width, height)
        VALUES (25 + (i - 1) % 4 * 5, 12 + (i - 1) % 4 * 2, 5.5)
        RETURNING id_size INTO v_size_id;

        INSERT INTO hockey_gates (depth, length, width, height, grid, quantity)
        VALUES (1.12, 1.83, 1.83, 1.22, i % 10 <> 0, 1 + i % 2)
        RETURNING id_hockey_gates INTO v_gates_id;

        INSERT INTO cones_pack (quantity, length, width, height)
        VALUES (10 + (i - 1) % 5 * 5, 0.20, 0.20, 0.30)
        RETURNING id_cones_pack INTO v_cones_id;

        INSERT INTO pack_washers (quantity, length, width, weight_kg, height, type)
        VALUES (
            20 + (i - 1) % 5 * 5, 0.076, 0.076,
            CASE WHEN i % 2 = 0 THEN 0.170 ELSE 0.160 END,
            0.025,
            CASE WHEN i % 2 = 0 THEN 'Игровая' ELSE 'Тренировочная' END
        )
        RETURNING id_pack_washers INTO v_washers_id;

        INSERT INTO sports_equipment (id_hockey_gates, id_cones_pack, id_pack_washers)
        VALUES (v_gates_id, v_cones_id, v_washers_id)
        RETURNING id_sports_equipment INTO v_equipment_id;

        INSERT INTO training_zone (id_size, id_sports_equipment, number)
        VALUES (v_size_id, v_equipment_id, i)
        RETURNING id_training_zone INTO v_zone_id;

        INSERT INTO service (id_training_zone)
        VALUES (v_zone_id)
        RETURNING id_service INTO v_service_id;

        -- 100 сеансов: по 5 в день с 1 по 20 ноября 2026 года.
        v_start := TIMESTAMP '2026-11-01 08:00:00'
            + ((i - 1) / 5) * INTERVAL '1 day'
            + ((i - 1) % 5) * INTERVAL '2 hours';
        v_minutes := CASE WHEN i % 2 = 0 THEN 90 ELSE 60 END;
        v_price := 1000 + (i - 1) % 10 * 150;

        -- duration вычисляется самой БД; передавать его не нужно.
        INSERT INTO rent_session (id_service, price, start_time, end_time)
        VALUES (
            v_service_id, v_price, v_start,
            v_start + v_minutes * INTERVAL '1 minute'
        )
        RETURNING id_rent_session INTO v_session_id;
        v_session_ids[i] := v_session_id;
        v_session_prices[i] := v_price;
    END LOOP;

    -- 100 заказов распределяем между 75 людьми и 80 сеансами.
    -- Есть повторные заказы людей и свободные сеансы для проверки запросов.
    FOR i IN 1..100 LOOP
        v_session_index := 1 + (i - 1) % 80;

        INSERT INTO accept (condition)
        VALUES (v_conditions[1 + (i - 1) % 3])
        RETURNING id_accept INTO v_accept_id;

        INSERT INTO reserve_on_order (price)
        VALUES (v_session_prices[v_session_index])
        RETURNING id_reserve_on_order INTO v_reserve_id;

        INSERT INTO order_on_rent (
            id_human, id_accept, id_reserve_on_order, id_rent_session
        )
        VALUES (
            v_human_ids[1 + (i - 1) % 75],
            v_accept_id, v_reserve_id, v_session_ids[v_session_index]
        );
    END LOOP;

    -- Проверяем точное количество строк до фиксации транзакции.
    FOREACH v_table IN ARRAY v_tables LOOP
        EXECUTE format('SELECT COUNT(*) FROM public.%I', v_table) INTO v_count;
        IF v_count <> 100 THEN
            RAISE EXCEPTION
                'Таблица public.%: ожидалось 100 строк, получено %.',
                v_table, v_count;
        END IF;
    END LOOP;
END;
$$;

COMMIT;

-- Data Output: 12 строк; в каждой строке row_count = 100.
-- Считаем каждую таблицу отдельно, чтобы соединения не умножали строки.
SELECT 'human' AS table_name, COUNT(*) AS row_count FROM public.human
UNION ALL SELECT 'size', COUNT(*) FROM public.size
UNION ALL SELECT 'hockey_gates', COUNT(*) FROM public.hockey_gates
UNION ALL SELECT 'cones_pack', COUNT(*) FROM public.cones_pack
UNION ALL SELECT 'pack_washers', COUNT(*) FROM public.pack_washers
UNION ALL SELECT 'sports_equipment', COUNT(*) FROM public.sports_equipment
UNION ALL SELECT 'training_zone', COUNT(*) FROM public.training_zone
UNION ALL SELECT 'service', COUNT(*) FROM public.service
UNION ALL SELECT 'rent_session', COUNT(*) FROM public.rent_session
UNION ALL SELECT 'accept', COUNT(*) FROM public.accept
UNION ALL SELECT 'reserve_on_order', COUNT(*) FROM public.reserve_on_order
UNION ALL SELECT 'order_on_rent', COUNT(*) FROM public.order_on_rent
ORDER BY table_name;