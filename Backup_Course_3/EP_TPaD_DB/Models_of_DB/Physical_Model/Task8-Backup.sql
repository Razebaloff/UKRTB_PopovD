--
-- PostgreSQL database dump
--

\restrict rC1AI0KEHvHXdzchakEqHsOW1KnIMSALgsWvGgeuNMxr0QYyoF6axCd5ibVlBfl

-- Dumped from database version 18.6 (Debian 18.6-1.pgdg13+2)
-- Dumped by pg_dump version 18.4

-- Started on 2026-10-28 15:27:44

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 224 (class 1259 OID 16424)
-- Name: GameSession_Table; Type: TABLE; Schema: public; Owner: user1
--

CREATE TABLE public."GameSession_Table" (
    "SessionId_PK" integer NOT NULL,
    "UserId_FK" integer NOT NULL,
    "SessionDate" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "Title" character varying(150),
    "IsClosed" boolean DEFAULT false NOT NULL
);


ALTER TABLE public."GameSession_Table" OWNER TO user1;

--
-- TOC entry 223 (class 1259 OID 16423)
-- Name: GameSession_Table_SessionId_PK_seq; Type: SEQUENCE; Schema: public; Owner: user1
--

CREATE SEQUENCE public."GameSession_Table_SessionId_PK_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."GameSession_Table_SessionId_PK_seq" OWNER TO user1;

--
-- TOC entry 3501 (class 0 OID 0)
-- Dependencies: 223
-- Name: GameSession_Table_SessionId_PK_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: user1
--

ALTER SEQUENCE public."GameSession_Table_SessionId_PK_seq" OWNED BY public."GameSession_Table"."SessionId_PK";


--
-- TOC entry 222 (class 1259 OID 16409)
-- Name: Hero_Table; Type: TABLE; Schema: public; Owner: user1
--

CREATE TABLE public."Hero_Table" (
    "HeroId_PK" integer NOT NULL,
    "HeroName" character varying(100) NOT NULL,
    "PrimaryAttribute" character varying(20) NOT NULL,
    "IsActive" boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_hero_attribute CHECK ((("PrimaryAttribute")::text = ANY ((ARRAY['Strength'::character varying, 'Agility'::character varying, 'Intelligence'::character varying, 'Universal'::character varying])::text[])))
);


ALTER TABLE public."Hero_Table" OWNER TO user1;

--
-- TOC entry 221 (class 1259 OID 16408)
-- Name: Hero_Table_HeroId_PK_seq; Type: SEQUENCE; Schema: public; Owner: user1
--

CREATE SEQUENCE public."Hero_Table_HeroId_PK_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Hero_Table_HeroId_PK_seq" OWNER TO user1;

--
-- TOC entry 3502 (class 0 OID 0)
-- Dependencies: 221
-- Name: Hero_Table_HeroId_PK_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: user1
--

ALTER SEQUENCE public."Hero_Table_HeroId_PK_seq" OWNED BY public."Hero_Table"."HeroId_PK";


--
-- TOC entry 226 (class 1259 OID 16442)
-- Name: Match_Table; Type: TABLE; Schema: public; Owner: user1
--

CREATE TABLE public."Match_Table" (
    "MatchId_PK" integer NOT NULL,
    "SessionId_FK" integer NOT NULL,
    "HeroId_FK" integer NOT NULL,
    "CheckedByUserId_FK" integer,
    "MatchResult" character varying(10) NOT NULL,
    "MmrChange" integer DEFAULT 0 NOT NULL,
    "Status" character varying(20) DEFAULT 'Draft'::character varying NOT NULL,
    "ManagerComment" character varying(500),
    "SubmittedAt" timestamp without time zone,
    "ReviewedAt" timestamp without time zone,
    CONSTRAINT chk_match_result CHECK ((("MatchResult")::text = ANY ((ARRAY['Win'::character varying, 'Lose'::character varying])::text[]))),
    CONSTRAINT chk_match_status CHECK ((("Status")::text = ANY ((ARRAY['Draft'::character varying, 'OnReview'::character varying, 'Approved'::character varying, 'Rejected'::character varying])::text[])))
);


ALTER TABLE public."Match_Table" OWNER TO user1;

--
-- TOC entry 225 (class 1259 OID 16441)
-- Name: Match_Table_MatchId_PK_seq; Type: SEQUENCE; Schema: public; Owner: user1
--

CREATE SEQUENCE public."Match_Table_MatchId_PK_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."Match_Table_MatchId_PK_seq" OWNER TO user1;

--
-- TOC entry 3503 (class 0 OID 0)
-- Dependencies: 225
-- Name: Match_Table_MatchId_PK_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: user1
--

ALTER SEQUENCE public."Match_Table_MatchId_PK_seq" OWNED BY public."Match_Table"."MatchId_PK";


--
-- TOC entry 220 (class 1259 OID 16390)
-- Name: User_Table; Type: TABLE; Schema: public; Owner: user1
--

CREATE TABLE public."User_Table" (
    "UserId_PK" integer NOT NULL,
    "Login" character varying(50) NOT NULL,
    "PasswordHash" character varying(255) NOT NULL,
    "Role" character varying(20) DEFAULT 'Player'::character varying NOT NULL,
    "StartMmr" integer DEFAULT 0 NOT NULL,
    "CreatedAt" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CONSTRAINT chk_user_role CHECK ((("Role")::text = ANY ((ARRAY['Player'::character varying, 'Manager'::character varying, 'Admin'::character varying])::text[])))
);


ALTER TABLE public."User_Table" OWNER TO user1;

--
-- TOC entry 219 (class 1259 OID 16389)
-- Name: User_Table_UserId_PK_seq; Type: SEQUENCE; Schema: public; Owner: user1
--

CREATE SEQUENCE public."User_Table_UserId_PK_seq"
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public."User_Table_UserId_PK_seq" OWNER TO user1;

--
-- TOC entry 3504 (class 0 OID 0)
-- Dependencies: 219
-- Name: User_Table_UserId_PK_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: user1
--

ALTER SEQUENCE public."User_Table_UserId_PK_seq" OWNED BY public."User_Table"."UserId_PK";


--
-- TOC entry 3310 (class 2604 OID 16427)
-- Name: GameSession_Table SessionId_PK; Type: DEFAULT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."GameSession_Table" ALTER COLUMN "SessionId_PK" SET DEFAULT nextval('public."GameSession_Table_SessionId_PK_seq"'::regclass);


--
-- TOC entry 3308 (class 2604 OID 16412)
-- Name: Hero_Table HeroId_PK; Type: DEFAULT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Hero_Table" ALTER COLUMN "HeroId_PK" SET DEFAULT nextval('public."Hero_Table_HeroId_PK_seq"'::regclass);


--
-- TOC entry 3313 (class 2604 OID 16445)
-- Name: Match_Table MatchId_PK; Type: DEFAULT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Match_Table" ALTER COLUMN "MatchId_PK" SET DEFAULT nextval('public."Match_Table_MatchId_PK_seq"'::regclass);


--
-- TOC entry 3304 (class 2604 OID 16393)
-- Name: User_Table UserId_PK; Type: DEFAULT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."User_Table" ALTER COLUMN "UserId_PK" SET DEFAULT nextval('public."User_Table_UserId_PK_seq"'::regclass);


--
-- TOC entry 3493 (class 0 OID 16424)
-- Dependencies: 224
-- Data for Name: GameSession_Table; Type: TABLE DATA; Schema: public; Owner: user1
--

COPY public."GameSession_Table" ("SessionId_PK", "UserId_FK", "SessionDate", "Title", "IsClosed") FROM stdin;
1	1	2026-10-28 09:54:16.75542	Калибровка и мейн сессия	f
\.


--
-- TOC entry 3491 (class 0 OID 16409)
-- Dependencies: 222
-- Data for Name: Hero_Table; Type: TABLE DATA; Schema: public; Owner: user1
--

COPY public."Hero_Table" ("HeroId_PK", "HeroName", "PrimaryAttribute", "IsActive") FROM stdin;
1	Pudge	Strength	t
2	Shadow Fiend	Agility	t
3	Invoker	Intelligence	t
4	Anti-Mage	Agility	t
5	Juggernaut	Agility	t
6	Crystal Maiden	Intelligence	t
7	Muerta	Intelligence	t
8	Marci	Universal	t
9	Sand King	Strength	t
10	Slark	Agility	t
\.


--
-- TOC entry 3495 (class 0 OID 16442)
-- Dependencies: 226
-- Data for Name: Match_Table; Type: TABLE DATA; Schema: public; Owner: user1
--

COPY public."Match_Table" ("MatchId_PK", "SessionId_FK", "HeroId_FK", "CheckedByUserId_FK", "MatchResult", "MmrChange", "Status", "ManagerComment", "SubmittedAt", "ReviewedAt") FROM stdin;
1	1	7	\N	Lose	-27	Draft	\N	2026-10-28 08:54:16.75542	\N
2	1	10	\N	Lose	-24	Draft	\N	2026-10-28 07:54:16.75542	\N
3	1	1	\N	Lose	-25	Draft	\N	2026-10-28 06:54:16.75542	\N
4	1	6	\N	Win	24	Draft	\N	2026-10-28 05:54:16.75542	\N
5	1	7	\N	Win	24	Draft	\N	2026-10-28 04:54:16.75542	\N
6	1	10	\N	Win	25	Draft	\N	2026-10-28 03:54:16.75542	\N
7	1	5	\N	Lose	-25	Draft	\N	2026-10-28 02:54:16.75542	\N
8	1	8	\N	Lose	-25	Draft	\N	2026-10-28 01:54:16.75542	\N
9	1	7	\N	Win	25	Draft	\N	2026-10-28 00:54:16.75542	\N
10	1	4	\N	Win	25	Draft	\N	2026-10-27 23:54:16.75542	\N
11	1	1	\N	Win	27	OnReview	\N	2026-10-27 22:54:16.75542	\N
12	1	10	\N	Win	24	OnReview	\N	2026-10-27 21:54:16.75542	\N
13	1	4	\N	Win	24	OnReview	\N	2026-10-27 20:54:16.75542	\N
14	1	8	\N	Lose	-26	OnReview	\N	2026-10-27 19:54:16.75542	\N
15	1	1	\N	Win	24	OnReview	\N	2026-10-27 18:54:16.75542	\N
16	1	1	\N	Lose	-26	OnReview	\N	2026-10-27 17:54:16.75542	\N
17	1	1	\N	Win	24	OnReview	\N	2026-10-27 16:54:16.75542	\N
18	1	6	\N	Lose	-27	OnReview	\N	2026-10-27 15:54:16.75542	\N
19	1	5	\N	Lose	-27	OnReview	\N	2026-10-27 14:54:16.75542	\N
20	1	10	\N	Lose	-25	OnReview	\N	2026-10-27 13:54:16.75542	\N
21	1	1	\N	Win	27	OnReview	\N	2026-10-27 12:54:16.75542	\N
22	1	2	\N	Win	25	OnReview	\N	2026-10-27 11:54:16.75542	\N
23	1	9	\N	Win	25	OnReview	\N	2026-10-27 10:54:16.75542	\N
24	1	1	\N	Lose	-25	OnReview	\N	2026-10-27 09:54:16.75542	\N
25	1	1	\N	Win	27	OnReview	\N	2026-10-27 08:54:16.75542	\N
26	1	10	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-27 07:54:16.75542	2026-10-27 08:09:16.75542
27	1	8	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-27 06:54:16.75542	2026-10-27 07:09:16.75542
28	1	1	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-27 05:54:16.75542	2026-10-27 06:09:16.75542
29	1	2	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-27 04:54:16.75542	2026-10-27 05:09:16.75542
30	1	1	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-27 03:54:16.75542	2026-10-27 04:09:16.75542
31	1	7	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-27 02:54:16.75542	2026-10-27 03:09:16.75542
32	1	3	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-27 01:54:16.75542	2026-10-27 02:09:16.75542
33	1	5	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-27 00:54:16.75542	2026-10-27 01:09:16.75542
34	1	8	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-26 23:54:16.75542	2026-10-27 00:09:16.75542
35	1	8	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-26 22:54:16.75542	2026-10-26 23:09:16.75542
36	1	5	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-26 21:54:16.75542	2026-10-26 22:09:16.75542
37	1	6	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-26 20:54:16.75542	2026-10-26 21:09:16.75542
38	1	5	2	Win	26	Approved	Отличный макроконтроль и закуп.	2026-10-26 19:54:16.75542	2026-10-26 20:09:16.75542
39	1	6	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-26 18:54:16.75542	2026-10-26 19:09:16.75542
40	1	8	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-26 17:54:16.75542	2026-10-26 18:09:16.75542
41	1	7	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-26 16:54:16.75542	2026-10-26 17:09:16.75542
42	1	5	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-26 15:54:16.75542	2026-10-26 16:09:16.75542
43	1	8	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-26 14:54:16.75542	2026-10-26 15:09:16.75542
44	1	4	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-26 13:54:16.75542	2026-10-26 14:09:16.75542
45	1	3	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-26 12:54:16.75542	2026-10-26 13:09:16.75542
46	1	1	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-26 11:54:16.75542	2026-10-26 12:09:16.75542
47	1	7	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-26 10:54:16.75542	2026-10-26 11:09:16.75542
48	1	9	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-26 09:54:16.75542	2026-10-26 10:09:16.75542
49	1	2	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-26 08:54:16.75542	2026-10-26 09:09:16.75542
50	1	1	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-26 07:54:16.75542	2026-10-26 08:09:16.75542
51	1	6	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-26 06:54:16.75542	2026-10-26 07:09:16.75542
52	1	8	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-26 05:54:16.75542	2026-10-26 06:09:16.75542
53	1	10	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-26 04:54:16.75542	2026-10-26 05:09:16.75542
54	1	3	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-26 03:54:16.75542	2026-10-26 04:09:16.75542
55	1	4	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-26 02:54:16.75542	2026-10-26 03:09:16.75542
56	1	5	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-26 01:54:16.75542	2026-10-26 02:09:16.75542
57	1	8	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-26 00:54:16.75542	2026-10-26 01:09:16.75542
58	1	9	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-25 23:54:16.75542	2026-10-26 00:09:16.75542
59	1	3	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-25 22:54:16.75542	2026-10-25 23:09:16.75542
60	1	6	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-25 21:54:16.75542	2026-10-25 22:09:16.75542
61	1	6	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-25 20:54:16.75542	2026-10-25 21:09:16.75542
62	1	8	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-25 19:54:16.75542	2026-10-25 20:09:16.75542
63	1	5	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-25 18:54:16.75542	2026-10-25 19:09:16.75542
64	1	3	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-25 17:54:16.75542	2026-10-25 18:09:16.75542
65	1	8	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-25 16:54:16.75542	2026-10-25 17:09:16.75542
66	1	8	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-25 15:54:16.75542	2026-10-25 16:09:16.75542
67	1	2	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-25 14:54:16.75542	2026-10-25 15:09:16.75542
68	1	5	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-25 13:54:16.75542	2026-10-25 14:09:16.75542
69	1	10	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-25 12:54:16.75542	2026-10-25 13:09:16.75542
70	1	7	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-25 11:54:16.75542	2026-10-25 12:09:16.75542
71	1	2	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-25 10:54:16.75542	2026-10-25 11:09:16.75542
72	1	1	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-25 09:54:16.75542	2026-10-25 10:09:16.75542
73	1	2	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-25 08:54:16.75542	2026-10-25 09:09:16.75542
74	1	8	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-25 07:54:16.75542	2026-10-25 08:09:16.75542
75	1	3	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-25 06:54:16.75542	2026-10-25 07:09:16.75542
76	1	4	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-25 05:54:16.75542	2026-10-25 06:09:16.75542
77	1	8	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-25 04:54:16.75542	2026-10-25 05:09:16.75542
78	1	2	2	Win	26	Approved	Отличный макроконтроль и закуп.	2026-10-25 03:54:16.75542	2026-10-25 04:09:16.75542
79	1	4	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-25 02:54:16.75542	2026-10-25 03:09:16.75542
80	1	7	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-25 01:54:16.75542	2026-10-25 02:09:16.75542
81	1	1	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-25 00:54:16.75542	2026-10-25 01:09:16.75542
82	1	6	2	Lose	-27	Approved	Отличный макроконтроль и закуп.	2026-10-24 23:54:16.75542	2026-10-25 00:09:16.75542
83	1	5	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-24 22:54:16.75542	2026-10-24 23:09:16.75542
84	1	6	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-24 21:54:16.75542	2026-10-24 22:09:16.75542
85	1	6	2	Lose	-24	Approved	Отличный макроконтроль и закуп.	2026-10-24 20:54:16.75542	2026-10-24 21:09:16.75542
86	1	7	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-24 19:54:16.75542	2026-10-24 20:09:16.75542
87	1	8	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-24 18:54:16.75542	2026-10-24 19:09:16.75542
88	1	8	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-24 17:54:16.75542	2026-10-24 18:09:16.75542
89	1	4	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-24 16:54:16.75542	2026-10-24 17:09:16.75542
90	1	8	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-24 15:54:16.75542	2026-10-24 16:09:16.75542
91	1	10	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-24 14:54:16.75542	2026-10-24 15:09:16.75542
92	1	3	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-24 13:54:16.75542	2026-10-24 14:09:16.75542
93	1	7	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-24 12:54:16.75542	2026-10-24 13:09:16.75542
94	1	8	2	Win	26	Approved	Отличный макроконтроль и закуп.	2026-10-24 11:54:16.75542	2026-10-24 12:09:16.75542
95	1	3	2	Lose	-25	Approved	Отличный макроконтроль и закуп.	2026-10-24 10:54:16.75542	2026-10-24 11:09:16.75542
96	1	3	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-24 09:54:16.75542	2026-10-24 10:09:16.75542
97	1	9	2	Lose	-26	Approved	Отличный макроконтроль и закуп.	2026-10-24 08:54:16.75542	2026-10-24 09:09:16.75542
98	1	5	2	Win	27	Approved	Отличный макроконтроль и закуп.	2026-10-24 07:54:16.75542	2026-10-24 08:09:16.75542
99	1	6	2	Win	24	Approved	Отличный макроконтроль и закуп.	2026-10-24 06:54:16.75542	2026-10-24 07:09:16.75542
100	1	2	2	Win	25	Approved	Отличный макроконтроль и закуп.	2026-10-24 05:54:16.75542	2026-10-24 06:09:16.75542
101	1	6	2	Lose	-25	Rejected	Ошибка ввода данных, матч не найден.	2026-10-24 04:54:16.75542	2026-10-24 05:09:16.75542
102	1	8	2	Lose	-24	Rejected	Ошибка ввода данных, матч не найден.	2026-10-24 03:54:16.75542	2026-10-24 04:09:16.75542
103	1	1	2	Lose	-25	Rejected	Ошибка ввода данных, матч не найден.	2026-10-24 02:54:16.75542	2026-10-24 03:09:16.75542
104	1	8	2	Win	24	Rejected	Ошибка ввода данных, матч не найден.	2026-10-24 01:54:16.75542	2026-10-24 02:09:16.75542
105	1	1	2	Lose	-25	Rejected	Ошибка ввода данных, матч не найден.	2026-10-24 00:54:16.75542	2026-10-24 01:09:16.75542
\.


--
-- TOC entry 3489 (class 0 OID 16390)
-- Dependencies: 220
-- Data for Name: User_Table; Type: TABLE DATA; Schema: public; Owner: user1
--

COPY public."User_Table" ("UserId_PK", "Login", "PasswordHash", "Role", "StartMmr", "CreatedAt") FROM stdin;
1	miracle_player	hashed_pass_1	Player	4500	2026-10-28 09:54:16.75542
2	silent_coach	hashed_pass_2	Manager	0	2026-10-28 09:54:16.75542
3	system_admin	hashed_pass_3	Admin	0	2026-10-28 09:54:16.75542
\.


--
-- TOC entry 3505 (class 0 OID 0)
-- Dependencies: 223
-- Name: GameSession_Table_SessionId_PK_seq; Type: SEQUENCE SET; Schema: public; Owner: user1
--

SELECT pg_catalog.setval('public."GameSession_Table_SessionId_PK_seq"', 1, true);


--
-- TOC entry 3506 (class 0 OID 0)
-- Dependencies: 221
-- Name: Hero_Table_HeroId_PK_seq; Type: SEQUENCE SET; Schema: public; Owner: user1
--

SELECT pg_catalog.setval('public."Hero_Table_HeroId_PK_seq"', 10, true);


--
-- TOC entry 3507 (class 0 OID 0)
-- Dependencies: 225
-- Name: Match_Table_MatchId_PK_seq; Type: SEQUENCE SET; Schema: public; Owner: user1
--

SELECT pg_catalog.setval('public."Match_Table_MatchId_PK_seq"', 105, true);


--
-- TOC entry 3508 (class 0 OID 0)
-- Dependencies: 219
-- Name: User_Table_UserId_PK_seq; Type: SEQUENCE SET; Schema: public; Owner: user1
--

SELECT pg_catalog.setval('public."User_Table_UserId_PK_seq"', 3, true);


--
-- TOC entry 3329 (class 2606 OID 16435)
-- Name: GameSession_Table GameSession_Table_pkey; Type: CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."GameSession_Table"
    ADD CONSTRAINT "GameSession_Table_pkey" PRIMARY KEY ("SessionId_PK");


--
-- TOC entry 3325 (class 2606 OID 16422)
-- Name: Hero_Table Hero_Table_HeroName_key; Type: CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Hero_Table"
    ADD CONSTRAINT "Hero_Table_HeroName_key" UNIQUE ("HeroName");


--
-- TOC entry 3327 (class 2606 OID 16420)
-- Name: Hero_Table Hero_Table_pkey; Type: CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Hero_Table"
    ADD CONSTRAINT "Hero_Table_pkey" PRIMARY KEY ("HeroId_PK");


--
-- TOC entry 3332 (class 2606 OID 16459)
-- Name: Match_Table Match_Table_pkey; Type: CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Match_Table"
    ADD CONSTRAINT "Match_Table_pkey" PRIMARY KEY ("MatchId_PK");


--
-- TOC entry 3321 (class 2606 OID 16407)
-- Name: User_Table User_Table_Login_key; Type: CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."User_Table"
    ADD CONSTRAINT "User_Table_Login_key" UNIQUE ("Login");


--
-- TOC entry 3323 (class 2606 OID 16405)
-- Name: User_Table User_Table_pkey; Type: CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."User_Table"
    ADD CONSTRAINT "User_Table_pkey" PRIMARY KEY ("UserId_PK");


--
-- TOC entry 3333 (class 1259 OID 16477)
-- Name: idx_match_hero; Type: INDEX; Schema: public; Owner: user1
--

CREATE INDEX idx_match_hero ON public."Match_Table" USING btree ("HeroId_FK");


--
-- TOC entry 3334 (class 1259 OID 16478)
-- Name: idx_match_manager; Type: INDEX; Schema: public; Owner: user1
--

CREATE INDEX idx_match_manager ON public."Match_Table" USING btree ("CheckedByUserId_FK");


--
-- TOC entry 3335 (class 1259 OID 16476)
-- Name: idx_match_session; Type: INDEX; Schema: public; Owner: user1
--

CREATE INDEX idx_match_session ON public."Match_Table" USING btree ("SessionId_FK");


--
-- TOC entry 3336 (class 1259 OID 16479)
-- Name: idx_match_status; Type: INDEX; Schema: public; Owner: user1
--

CREATE INDEX idx_match_status ON public."Match_Table" USING btree ("Status");


--
-- TOC entry 3330 (class 1259 OID 16475)
-- Name: idx_session_user; Type: INDEX; Schema: public; Owner: user1
--

CREATE INDEX idx_session_user ON public."GameSession_Table" USING btree ("UserId_FK");


--
-- TOC entry 3338 (class 2606 OID 16465)
-- Name: Match_Table fk_match_hero; Type: FK CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Match_Table"
    ADD CONSTRAINT fk_match_hero FOREIGN KEY ("HeroId_FK") REFERENCES public."Hero_Table"("HeroId_PK") ON DELETE RESTRICT;


--
-- TOC entry 3339 (class 2606 OID 16470)
-- Name: Match_Table fk_match_manager; Type: FK CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Match_Table"
    ADD CONSTRAINT fk_match_manager FOREIGN KEY ("CheckedByUserId_FK") REFERENCES public."User_Table"("UserId_PK") ON DELETE SET NULL;


--
-- TOC entry 3340 (class 2606 OID 16460)
-- Name: Match_Table fk_match_session; Type: FK CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."Match_Table"
    ADD CONSTRAINT fk_match_session FOREIGN KEY ("SessionId_FK") REFERENCES public."GameSession_Table"("SessionId_PK") ON DELETE CASCADE;


--
-- TOC entry 3337 (class 2606 OID 16436)
-- Name: GameSession_Table fk_session_user; Type: FK CONSTRAINT; Schema: public; Owner: user1
--

ALTER TABLE ONLY public."GameSession_Table"
    ADD CONSTRAINT fk_session_user FOREIGN KEY ("UserId_FK") REFERENCES public."User_Table"("UserId_PK") ON UPDATE CASCADE ON DELETE CASCADE;


-- Completed on 2026-10-28 15:27:44

--
-- PostgreSQL database dump complete
--

\unrestrict rC1AI0KEHvHXdzchakEqHsOW1KnIMSALgsWvGgeuNMxr0QYyoF6axCd5ibVlBfl

