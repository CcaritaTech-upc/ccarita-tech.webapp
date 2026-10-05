export default {
  paths: ['../test/Features/**/*.feature'],
  import: ['tests/bdd/support/**/*.js', 'tests/bdd/steps/**/*.js'],
  format: ['progress'],
  publishQuiet: true,
  parallel: 1,
};
