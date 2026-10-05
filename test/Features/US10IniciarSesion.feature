@US10
Feature: US10 Iniciar Sesión (Login)
  Como usuario
  quiero ingresar mis credenciales
  para acceder a mi cuenta y utilizar las funciones protegidas según mi rol.

  Scenario Outline: Inicio de sesión exitoso con credenciales correctas
    Given el usuario de prueba con rol <rol> y alias <alias> tiene una cuenta registrada
    When inicia sesión con la contraseña correcta "<password>"
    Then la API de sesiones responderá con código 201 Created
    And el usuario tendrá una sesión activa y llegará a "/analytics/dashboard"
    Examples:
      | rol | alias | password |
      | Builder | builder | secret123 |
      | Owner | owner | secret123 |

  Scenario Outline: Intento de inicio de sesión con contraseña incorrecta
    Given el usuario de prueba con rol <rol> y alias <alias> tiene una cuenta registrada
    When intenta iniciar sesión con la contraseña incorrecta "<password_incorrecto>"
    Then la API de sesiones responderá con código 401 Unauthorized
    And el login mostrará el mensaje "Correo o contraseña incorrectos."
    Examples:
      | rol | alias | password_incorrecto |
      | Builder | builder | claveIncorrecta99 |
      | Owner | owner | 12345 |
