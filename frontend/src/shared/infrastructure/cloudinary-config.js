/**
 * Configuraciones personalizadas para el widget de subida de Cloudinary
 * alineadas con la identidad visual de IoBuild (Verde Esmeralda #10B981, Inter font, Español).
 */

const ioBuildPalette = {
    window: "#FFFFFF",
    windowBorder: "#E5E7EB",
    tabIcon: "#10B981",
    menuIcons: "#374151",
    textDark: "#111827",
    textLight: "#FFFFFF",
    link: "#10B981",
    action: "#10B981",
    inactiveTabIcon: "#9CA3AF",
    error: "#EF4444",
    inProgress: "#10B981",
    complete: "#059669",
    sourceBg: "#F9FAFB"
};

const ioBuildStyles = {
    palette: ioBuildPalette,
    fonts: {
        default: null,
        "'Inter', sans-serif": {
            url: "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap",
            active: true
        }
    }
};

/**
 * Configuración para avatares de perfil (Constructor y Residente)
 * - Recorte cuadrado 1:1
 * - Cierre automático al completar
 * - Idioma español y paleta verde IoBuild
 */
export function getAvatarUploadConfig(cloudName, uploadPreset) {
    return {
        cloud_name: cloudName,
        upload_preset: uploadPreset,
        sources: ['local', 'camera', 'url'],
        multiple: false,
        resourceType: 'image',
        language: 'es',
        text: {
            es: {
                menu: {
                    files: "Mis Archivos",
                    web: "Dirección Web",
                    camera: "Cámara"
                },
                local: {
                    browse: "Explorar",
                    dd_title_single: "Arrastra y suelta tu foto de perfil aquí",
                    drop_title_single: "Suelta la imagen para subir"
                },
                queue: {
                    title: "Subiendo imagen...",
                    title_uploading_with_counter: "Subiendo {{num}} imagen",
                    title_processing_with_counter: "Procesando imagen...",
                    done: "Listo"
                },
                crop: {
                    title: "Ajusta y recorta tu foto",
                    crop_btn: "Recortar y Guardar",
                    skip_btn: "Usar sin recortar",
                    reset_btn: "Reiniciar",
                    close_btn: "Cerrar"
                }
            }
        },
        singleUploadAutoClose: true,
        cropping: false,
        clientAllowedFormats: ['png', 'jpg', 'jpeg', 'webp'],
        maxFileSize: 5 * 1024 * 1024, // 5MB
        styles: ioBuildStyles
    };
}

/**
 * Configuración para fotos de proyectos/edificios
 * - Cierre automático al completar
 * - Proporción libre / panorámica
 * - Idioma español y paleta verde IoBuild
 */
export function getProjectImageUploadConfig(cloudName, uploadPreset) {
    return {
        cloud_name: cloudName,
        upload_preset: uploadPreset,
        sources: ['local', 'camera', 'url'],
        multiple: false,
        resourceType: 'image',
        language: 'es',
        text: {
            es: {
                menu: {
                    files: "Mis Archivos",
                    web: "Dirección Web",
                    camera: "Cámara"
                },
                local: {
                    browse: "Explorar",
                    dd_title_single: "Arrastra y suelta la imagen del proyecto aquí",
                    drop_title_single: "Suelta la imagen para subir"
                },
                queue: {
                    title: "Subiendo portada...",
                    done: "Listo"
                }
            }
        },
        singleUploadAutoClose: true,
        cropping: false,
        clientAllowedFormats: ['png', 'jpg', 'jpeg', 'webp'],
        maxFileSize: 10 * 1024 * 1024, // 10MB
        styles: ioBuildStyles
    };
}
