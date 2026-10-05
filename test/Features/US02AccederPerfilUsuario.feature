@US02
Feature: US02 Acceder al perfil del usuario
  Como usuario
  quiero tener acceso a mi perfil
  para ver datos como mi nombre, email, número de teléfono y dirección.

  Scenario Outline: Visualización correcta de datos personales de perfil
    Given el usuario de prueba con rol <rol> y alias <alias> tiene el nombre "<nombre>" y teléfono "<telefono>"
    When abre su perfil desde el enlace de usuario
    Then el perfil mostrará nombre "<nombre>", correo de su cuenta y teléfono "<telefono>"
    And se mostrará el rol asignado "<rol_visible>"
    Examples:
      | rol | alias | nombre | telefono | rol_visible |
      | Builder | axel | Axel Ordoñez | 987654321 | Constructor |
      | Owner | roberto | Roberto Ccarita | 912345678 | Propietario |

  Scenario: Intento de acceso al perfil sin sesión activa
    Given un visitante sin sesión iniciada
    When navega a la ruta protegida "/profiles/profile"
    Then la aplicación redirigirá al usuario a "/iam/login"
    And mostrará la pantalla de inicio de sesión
