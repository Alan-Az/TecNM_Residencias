-- ==============================================================================
-- Migración 05: Adaptación de TecNM Residencias v2 a TecNM_LoginControl (SSO)
-- Descripción:
--   1. Agrega external_user_id (UUID) en users para vincular con LoginControl.
--   2. Migra datos residuales de users.career_id a career_heads / user_careers.
--   3. Reemplaza la vista vw_search_users sin depender de users.control_number ni users.career_id.
--   4. Elimina password_hash, control_number y career_id de la tabla users.
-- Garantía: Completamente idempotente; preserva todos los perfiles y roles.
-- ==============================================================================

BEGIN;

-- 1. Vincular identidad externa con LoginControl (UUID)
ALTER TABLE users ADD COLUMN IF NOT EXISTS external_user_id UUID NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_external_user_id
    ON users (external_user_id)
    WHERE external_user_id IS NOT NULL;

-- 2. Migrar datos residuales de users.career_id (si la columna todavía existe)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'career_id'
    ) THEN
        -- CareerHeads sin fila en career_heads
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'career_heads') THEN
            INSERT INTO career_heads (user_id, career_id, full_name, is_active, is_visible, display_order, created_at, updated_at)
            SELECT
                u.id,
                u.career_id,
                COALESCE(NULLIF(TRIM(CONCAT(COALESCE(u.first_name, ''), ' ', COALESCE(u.last_name, ''), COALESCE(' ' || u.last_name_2, ''))), ''), u.email),
                u.is_active,
                TRUE,
                0,
                CURRENT_TIMESTAMP,
                CURRENT_TIMESTAMP
            FROM users u
            WHERE u.career_id IS NOT NULL
              AND LOWER(u.role) IN ('jefecarrera', 'jefe_carrera', 'careerhead')
              AND NOT EXISTS (SELECT 1 FROM career_heads ch WHERE ch.user_id = u.id);
        END IF;

        -- Coordinators sin fila en user_careers
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'user_careers') THEN
            INSERT INTO user_careers (user_id, career_id, is_active, is_visible, display_order, created_at, updated_at)
            SELECT
                u.id,
                u.career_id,
                TRUE,
                TRUE,
                0,
                CURRENT_TIMESTAMP,
                CURRENT_TIMESTAMP
            FROM users u
            WHERE u.career_id IS NOT NULL
              AND LOWER(u.role) IN ('coordinadora', 'coordinador', 'coordinator')
              AND NOT EXISTS (SELECT 1 FROM user_careers uc WHERE uc.user_id = u.id AND uc.career_id = u.career_id);
        END IF;
    END IF;
END $$;

-- 3. Actualizar vista vw_search_users antes de eliminar columnas en users
DROP VIEW IF EXISTS vw_search_users CASCADE;
CREATE OR REPLACE VIEW vw_search_users AS
SELECT
    u.id AS id,
    u.id AS user_id,
    COALESCE((SELECT s.control_number FROM students s WHERE s.user_id = u.id AND s.is_active = TRUE LIMIT 1), '') AS control_number,
    TRIM(CONCAT(COALESCE(u.first_name, ''), ' ', COALESCE(u.last_name, ''), COALESCE(' ' || u.last_name_2, ''))) AS full_name,
    COALESCE(u.email, '') AS email,
    u.role::text AS role,
    COALESCE(
        (SELECT c.name FROM students s JOIN careers c ON s.career_id = c.id WHERE s.user_id = u.id AND s.is_active = TRUE LIMIT 1),
        (SELECT c.name FROM career_heads ch JOIN careers c ON ch.career_id = c.id WHERE ch.user_id = u.id AND ch.is_active = TRUE LIMIT 1),
        (SELECT c.name FROM user_careers uc JOIN careers c ON uc.career_id = c.id WHERE uc.user_id = u.id LIMIT 1),
        ''
    ) AS career_name,
    u.is_active AS is_active
FROM users u;

-- 4. Limpiar columnas de contraseña local y datos académicos duplicados en users
DROP INDEX IF EXISTS idx_users_career_id;
ALTER TABLE users DROP CONSTRAINT IF EXISTS uq_users_control_number;
ALTER TABLE users DROP COLUMN IF EXISTS password_hash;
ALTER TABLE users DROP COLUMN IF EXISTS control_number;
ALTER TABLE users DROP COLUMN IF EXISTS career_id;

COMMIT;
