-- Задание 7. Краткие примеры основных SQL-команд для MyDataBase.
-- Выполнять файл целиком. Изменения показаны на временной таблице.
BEGIN;

-- SELECT: вывести всех людей.
SELECT * FROM public.human;

-- WHERE, AND, BETWEEN: сеансы за 1000–2000 руб. длительностью 60 минут.
SELECT * FROM public.rent_session WHERE price BETWEEN 1000 AND 2000 AND duration = INTERVAL '60 minutes';

-- LIKE: найти людей по началу фамилии.
SELECT * FROM public.human WHERE last_name LIKE 'Иван%';

-- ORDER BY, LIMIT: вывести 5 самых дешёвых сеансов.
SELECT * FROM public.rent_session ORDER BY price, start_time, id_rent_session LIMIT 5;

-- DISTINCT: вывести состояния без повторений.
SELECT DISTINCT condition FROM public.accept;

-- JOIN: соединить заказы с данными людей.
SELECT o.id_order_on_rent, h.last_name, h.name
FROM public.order_on_rent AS o JOIN public.human AS h ON h.id_human = o.id_human;

-- GROUP BY, COUNT, HAVING: найти людей, у которых больше одного заказа.
SELECT id_human, COUNT(*) AS orders_count
FROM public.order_on_rent GROUP BY id_human HAVING COUNT(*) > 1;

-- SUM, AVG, MIN, MAX: посчитать показатели цен сеансов.
SELECT SUM(price), AVG(price), MIN(price), MAX(price) FROM public.rent_session;

-- CREATE TABLE: создать временную учебную таблицу сеансов.
CREATE TEMP TABLE task_7_demo (id_rent_session INTEGER PRIMARY KEY, price NUMERIC(12,2));

-- INSERT INTO: добавить учебный сеанс.
INSERT INTO task_7_demo (id_rent_session, price) VALUES (1, 1500);

-- UPDATE: изменить цену учебного сеанса.
UPDATE task_7_demo SET price = 1800 WHERE id_rent_session = 1;

-- DELETE FROM: удалить учебный сеанс по ID.
DELETE FROM task_7_demo WHERE id_rent_session = 1;

-- ALTER TABLE: добавить столбец примечания.
ALTER TABLE task_7_demo ADD COLUMN note TEXT;

-- TRUNCATE: удалить все строки учебной таблицы, сохранив её структуру.
TRUNCATE TABLE task_7_demo;

-- DROP TABLE: удалить учебную таблицу.
DROP TABLE task_7_demo;

-- ROLLBACK: отменить транзакцию.
ROLLBACK;

-- BEGIN, COMMIT: начать и завершить транзакцию (в примере только чтение).
BEGIN;
SELECT COUNT(*) AS people_count FROM public.human;
COMMIT;

-- Итоговый SELECT после COMMIT: показать людей в Data Output.
SELECT * FROM public.human ORDER BY id_human;
