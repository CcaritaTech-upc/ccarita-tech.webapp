Feature: US02 Acceder al perfil del usuario
  Como usuario
  quiero tener acceso a mi perfil
  para ver datos como mi nombre, email, número de teléfono y mi dirección.

  Scenario Outline: Visualización correcta de datos personales de perfil
    Given el <usuario> autenticado se encuentra en la pantalla principal
    When hace clic en su avatar y selecciona la opción de perfil
    Then el sistema mostrará la información con nombre <nombre>, correo <email> y teléfono <telefono>
    And se mostrará el rol asignado <rol>
    Examples:
      | usuario | nombre | email | telefono | rol |
      | Constructor | Axel Ordoñez | axel@iobuild.pe | 987654321 | Builder |
      | Propietario | Roberto Ccarita | roberto@iobuild.pe | 912345678 | Owner |

  Scenario Outline: Intento de acceso a perfil sin sesión activa
    Given un visitante sin sesión iniciada intenta acceder a la ruta <ruta_perfil>
    When envía la solicitud de navegación
    Then el sistema interceptará la petición con error 401 Unauthorized
    And redirigirá automáticamente al usuario a <ruta_login>
    Examples:
      | ruta_perfil | ruta_login |
      | /profile | /auth/sign-in |
