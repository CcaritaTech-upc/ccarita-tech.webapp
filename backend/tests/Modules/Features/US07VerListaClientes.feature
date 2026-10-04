Feature: US07 Ver Lista de Clientes
  Como Arquitecto
  quiero ver una lista de todos los clientes
  para gestionar sus proyectos asociados y el estado de su cuenta.

  Scenario Outline: Visualización de catálogo de clientes asociados a proyectos
    Given que el usuario autenticado tiene clientes registrados en el sistema
    When accede a la sección de "Clientes"
    Then el sistema retornará la lista de clientes con nombre completo <nombre_cliente>, proyecto <proyecto> y estado de cuenta <estado_cuenta>
    And mostrará las acciones de Ver Perfil y Configuración
    Examples:
      | nombre_cliente | proyecto | estado_cuenta |
      | Alex Resident | North Tower | Pending |
      | Carla Flores | Edificio Panorama | Active |

  Scenario Outline: Visualización cuando no existen clientes registrados
    Given que no existen clientes registrados para el constructor con ID <builder_id>
    When solicita la lista de clientes
    Then el sistema responderá con una lista vacía y código 200 OK
    And mostrará el mensaje <mensaje_vacio>
    Examples:
      | builder_id | mensaje_vacio |
      | 5 | No se encontraron clientes asociados |
