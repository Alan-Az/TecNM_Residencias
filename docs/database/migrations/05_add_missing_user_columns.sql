-- ==============================================================================
-- Migración 05: Asegurar columnas 'control_number' y 'career_id' en la tabla 'users'
-- Idempotente: seguro para ejecutar múltiples veces sin error.
-- ==============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'users' AND column_name = 'control_number'
    ) THEN
        ALTER TABLE users ADD COLUMN control_number VARCHAR(20) NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'users' AND column_name = 'career_id'
    ) THEN
        ALTER TABLE users ADD COLUMN career_id BIGINT NULL REFERENCES careers(id) ON DELETE SET NULL;
    END IF;
END $$;
