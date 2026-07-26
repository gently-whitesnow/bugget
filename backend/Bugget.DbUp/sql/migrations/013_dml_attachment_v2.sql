CREATE OR REPLACE FUNCTION public.create_attachment_internal(_entity_id int, _attach_type int, _storage_key text, _storage_kind int, _creator_user_id text, _length_bytes bigint, _file_name text, _mime_type text)
    RETURNS TABLE(
        id int,
        attach_type int,
        entity_id int,
        storage_key text,
        storage_kind int,
        creator_user_id text,
        length_bytes bigint,
        file_name text,
        mime_type text,
        has_preview boolean,
        is_gzip_compressed boolean,
        created_at timestamp with time zone
    )
    AS $$
DECLARE
    new_attachment_id int;
BEGIN
    INSERT INTO public.attachments(entity_id, attach_type, storage_key, storage_kind, creator_user_id, length_bytes, file_name, mime_type, bug_id, path)
        VALUES (_entity_id, _attach_type, _storage_key, _storage_kind, _creator_user_id, _length_bytes, _file_name, _mime_type, _entity_id, _storage_key)
    RETURNING
        public.attachments.id INTO new_attachment_id;
    --  Возвращаем данные
    RETURN QUERY
    SELECT
        a.id,
        a.attach_type,
        a.entity_id,
        a.storage_key,
        a.storage_kind,
        a.creator_user_id,
        a.length_bytes,
        a.file_name,
        a.mime_type,
        a.has_preview,
        a.is_gzip_compressed,
        a.created_at
    FROM
        public.attachments a
    WHERE
        a.id = new_attachment_id;
END;
$$
LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION public.update_attachment_internal(_id int, _storage_key text, _storage_kind int, _length_bytes bigint, _file_name text, _mime_type text, _has_preview boolean, _is_gzip_compressed boolean)
    RETURNS TABLE(
        id int,
        attach_type int,
        entity_id int,
        storage_key text,
        storage_kind int,
        creator_user_id text,
        length_bytes bigint,
        file_name text,
        mime_type text,
        has_preview boolean,
        is_gzip_compressed boolean,
        created_at timestamp with time zone)
    LANGUAGE plpgsql
    AS $$
BEGIN
    RETURN query UPDATE
        public.attachments
    SET
        storage_key = _storage_key,
        storage_kind = _storage_kind,
        length_bytes = _length_bytes,
        file_name = _file_name,
        mime_type = _mime_type,
        has_preview = _has_preview,
        is_gzip_compressed = _is_gzip_compressed,
        updated_at = now(),
        path = _storage_key
    WHERE
        public.attachments.id = _id
    RETURNING
        public.attachments.id,
        public.attachments.attach_type,
        public.attachments.entity_id,
        public.attachments.storage_key,
        public.attachments.storage_kind,
        public.attachments.creator_user_id,
        public.attachments.length_bytes,
        public.attachments.file_name,
        public.attachments.mime_type,
        public.attachments.has_preview,
        public.attachments.is_gzip_compressed,
        public.attachments.created_at;
END;
$$;
