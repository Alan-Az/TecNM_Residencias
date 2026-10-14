-- ==============================================================================
-- Script: add_activities_and_advisory_extensions.sql
-- Descripción: Campos aditivos e idempotentes para:
--              1. Cronograma versátil (semana inicio, duración en semanas y días)
--              2. Sesiones de asesoría (asistencia del alumno y vinculación a documento)
-- ==============================================================================

BEGIN;

-- 1. Ampliación de weekly_activities para cálculo versátil de cronograma
ALTER TABLE weekly_activities ADD COLUMN IF NOT EXISTS start_week INT NULL DEFAULT 1;
ALTER TABLE weekly_activities ADD COLUMN IF NOT EXISTS estimated_weeks INT NULL DEFAULT 1;
ALTER TABLE weekly_activities ADD COLUMN IF NOT EXISTS estimated_days INT NULL DEFAULT 7;

-- 2. Ampliación de advisory_sessions para control de asistencia y ligadura de documento
ALTER TABLE advisory_sessions ADD COLUMN IF NOT EXISTS student_attended BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE advisory_sessions ADD COLUMN IF NOT EXISTS document_id BIGINT NULL;

-- 3. Llave foránea e índice seguro para documento en asesorías
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_advisory_sessions_documents'
    ) THEN
        ALTER TABLE advisory_sessions
        ADD CONSTRAINT fk_advisory_sessions_documents
        FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_advisory_sessions_document_id ON advisory_sessions(document_id);

-- 4. Revalidar actividades existentes sin semana de inicio asignada
UPDATE weekly_activities wa
SET start_week = COALESCE((
        SELECT MIN(wp.week_number)
        FROM weekly_progress wp
        WHERE wp.activity_id = wa.id AND wp.status NOT IN ('pending', 'pendiente')
    ), 1),
    estimated_weeks = GREATEST(1, COALESCE((
        SELECT (MAX(wp.week_number) - MIN(wp.week_number) + 1)
        FROM weekly_progress wp
        WHERE wp.activity_id = wa.id AND wp.status NOT IN ('pending', 'pendiente')
    ), 1)),
    estimated_days = GREATEST(1, COALESCE((
        SELECT (MAX(wp.week_number) - MIN(wp.week_number) + 1) * 7
        FROM weekly_progress wp
        WHERE wp.activity_id = wa.id AND wp.status NOT IN ('pending', 'pendiente')
    ), 7))
WHERE wa.start_week IS NULL OR wa.estimated_weeks IS NULL;

COMMIT;
