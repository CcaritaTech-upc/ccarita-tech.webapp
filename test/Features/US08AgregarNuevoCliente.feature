@US08
Feature: US08 Agregar un Nuevo Cliente
  Como arquitecto
  quiero agregar un nuevo cliente
  para registrarlo en un proyecto y opcionalmente asociarlo a una unidad.

  Scenario Outline: Registro satisfactorio de un nuevo cliente
    Given el constructor con alias <alias> tiene el proyecto "<proyecto>"
    When abre el formulario para agregar un cliente
    And ingresa nombre "<nombre>" y correo "<email>"
    And selecciona el proyecto "<proyecto>"
    And registra el cliente
    Then el API de clientes responderá con código 201 Created
    And el cliente "<nombre>" aparecerá en la lista con estado "Activo"
    Examples:
      | alias | proyecto | nombre | email |
      | clientenuevoa | Edificio Panorama | Carlos Mendoza | carlos@example.com |
      | clientenuevob | Mirador del Valle | Lucía Benavides | lucia@example.com |

  Scenario: Fallo en el registro por omisión de correo
    Given el constructor con alias clientefallido tiene el proyecto "Torre de Prueba"
    When abre el formulario para agregar un cliente
    And ingresa el nombre "Cliente de Prueba" sin correo
    And selecciona el proyecto "Torre de Prueba"
    And intenta registrar el cliente
    Then el formulario bloqueará la creación
    And mostrará la validación "El correo electrónico es obligatorio."
