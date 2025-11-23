const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const CssMinimizerPlugin = require('css-minimizer-webpack-plugin');
const TerserPlugin = require('terser-webpack-plugin');

module.exports = (env, argv) => {
    const isDevelopment = argv.mode === 'development';

    return {
        entry: './TypeScript/app.ts',
        
        output: {
            filename: 'app.js',
            path: path.resolve(__dirname, 'wwwroot/dist'),
            clean: true,
            library: {
                type: 'module',
            },
        },

        experiments: {
            outputModule: true,
        },

        devtool: isDevelopment ? 'source-map' : false,

        resolve: {
            extensions: ['.ts', '.js', '.css'],
        },

        module: {
            rules: [
                {
                    test: /\.ts$/,
                    use: 'ts-loader',
                    exclude: /node_modules/,
                    sideEffects: true,
                },
                {
                    test: /\.css$/,
                    use: [
                        MiniCssExtractPlugin.loader,
                        {
                            loader: 'css-loader',
                            options: {
                                importLoaders: 1,
                                sourceMap: isDevelopment,
                            },
                        },
                    ],
                    sideEffects: true,
                },
                {
                    test: /\.(woff|woff2|eot|ttf|otf)$/i,
                    type: 'asset/resource',
                    generator: {
                        filename: 'fonts/[name][ext]',
                    },
                },
            ],
        },

        plugins: [
            new MiniCssExtractPlugin({
                filename: 'app.css',
            }),
        ],

        optimization: {
            minimize: !isDevelopment,
            minimizer: [
                new TerserPlugin({
                    terserOptions: {
                        compress: {
                        },
                        mangle: true,
                    },
                    extractComments: false,
                }),
                new CssMinimizerPlugin(),
            ],
            sideEffects: true,
            usedExports: false,
        },

        performance: {
            hints: isDevelopment ? false : 'warning',
            maxAssetSize: 512000,
            maxEntrypointSize: 512000,
        },

        stats: {
            colors: true,
            modules: false,
            children: false,
        },
    };
};
