Feature: US10 Iniciar Sesión (Login)
  Como Usuario
  quiero ingresar mis credenciales (correo y contraseña)
  para acceder a mi cuenta y utilizar las funciones protegidas según mi rol.

  Scenario Outline: Inicio de sesión exitoso con credenciales correctas
    Given que el <usuario> se encuentra en la pantalla de inicio de sesión
    And hace clic en el botón "Ingresar"
    When ingresa su correo electrónico <correo> y su contraseña correcta <password>
    Then el sistema validará las credenciales y responderá con código 200 OK
    And retornará el token JWT y redirigirá al usuario a <ruta_dashboard>
    Examples:
      | usuario | correo | password | ruta_dashboard |
      | Constructor | builder@iobuild.pe | Password123! | /builder/dashboard |
      | Propietario | owner@iobuild.pe | Password456# | /owner/dashboard |

  Scenario Outline: Intento de inicio de sesión con contraseña incorrecta
    Given que el usuario se encuentra en la vista de login
    And hace clic en el botón "Ingresar"
    When ingresa el correo registrado <correo> y la contraseña errónea <password_incorrecto>
    Then el sistema denegará la autenticación con código 401 Unauthorized
    And mostrará el mensaje de alerta <mensaje_error>
    Examples:
      | correo | password_incorrecto | mensaje_error |
      | builder@iobuild.pe | claveIncorrecta99 | Usuario o contraseña incorrectos |
      | owner@iobuild.pe | 12345 | Usuario o contraseña incorrectos |
