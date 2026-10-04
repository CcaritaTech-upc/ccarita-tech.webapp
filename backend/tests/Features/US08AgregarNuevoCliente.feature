Feature: US08 Agregar un Nuevo Cliente
  Como Arquitecto
  quiero poder agregar un nuevo cliente
  para poder registrarlo en el sistema y asociarlo a una unidad de vivienda.

  Scenario Outline: Registro satisfactorio de un nuevo cliente
    Given que el usuario se encuentra en el modal de "Agregar Cliente"
    And hace clic en el botón "Registrar Cliente"
    When ingresa el nombre completo <nombre>, correo <email> y selecciona el proyecto <proyecto_id>
    Then el sistema creará el registro del cliente con código 201 Created
    And el nuevo cliente aparecerá reflejado en la lista con estado inicial <estado>
    Examples:
      | nombre | email | proyecto_id | estado |
      | Carlos Mendoza | carlos@example.com | 1 | Pending |
      | Lucía Benavides | lucia@example.com | 2 | Pending |

  Scenario Outline: Fallo en el registro por omisión de nombre o correo
    Given que el usuario intenta registrar un cliente sin ingresar el <campo_vacio>
    When hace clic en "Registrar Cliente"
    Then el sistema denegará la creación con código 400 Bad Request
    And mostrará la alerta <mensaje_error>
    Examples:
      | campo_vacio | mensaje_error |
      | nombre | El nombre completo es obligatorio |
      | email | Debe proporcionar un correo electrónico válido |
