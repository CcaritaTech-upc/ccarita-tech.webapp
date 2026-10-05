@US05
Feature: US05 Agregar un nuevo proyecto
  Como arquitecto
  quiero agregar un nuevo proyecto
  para registrar nuevos desarrollos inmobiliarios.

  Scenario Outline: Registro exitoso de un proyecto con datos completos
    Given el constructor con alias <alias> abre el formulario de nuevo proyecto
    When ingresa el nombre "<nombre_proyecto>", ubicación "<ubicacion>" y descripción "<descripcion>"
    And guarda el proyecto
    Then el endpoint de proyectos responderá con código 201 Created
    And el proyecto "<nombre_proyecto>" aparecerá en la lista
    Examples:
      | alias | nombre_proyecto | ubicacion | descripcion |
      | panorama | Edificio Panorama | Surco, Lima | Proyecto residencial en Surco, Lima |
      | mirador | Mirador del Valle | Cayma, Arequipa | Proyecto residencial en Cayma, Arequipa |

  Scenario Outline: Rechazo de registro por nombre obligatorio faltante
    Given el constructor con alias <alias> abre el formulario de nuevo proyecto
    When deja vacío el nombre y completa ubicación "<ubicacion>" y descripción "<descripcion>"
    And guarda el proyecto
    Then la validación bloqueará la creación del proyecto
    And mostrará el mensaje "El nombre del proyecto es obligatorio."
    Examples:
      | alias | ubicacion | descripcion |
      | sinnombre | San Isidro, Lima | Proyecto residencial de prueba |
